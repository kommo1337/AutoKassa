using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace AutoKassa.Services
{
    /// <summary>
    /// Вспомогательные операции с кредитными картами, выполняемые в рамках
    /// переданного <see cref="AppDbContext"/>. Не вызывают SaveChanges.
    /// </summary>
    internal static class CreditCardOperationHelper
    {
        /// <summary>
        /// Название системной категории расходов, используемой для погашения кредита.
        /// </summary>
        public const string RepaymentCategoryName = "Погашение кредита";

        /// <summary>
        /// Получить текущий долг по кредитной карте в рамках переданного контекста.
        /// </summary>
        /// <param name="excludeRepaymentTransactionId">
        /// Идентификатор операции-погашения, которую необходимо исключить из расчёта
        /// (например, при пересчёте долга перед распределением уже сохранённого погашения).
        /// </param>
        public static async Task<decimal> GetCurrentDebtAsync(
            AppDbContext context,
            int creditCardId,
            int? excludeRepaymentTransactionId = null,
            CancellationToken ct = default)
        {
            var card = await context.CreditCards
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == creditCardId, ct)
                .ConfigureAwait(false);

            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={creditCardId} не найдена");

            var purchasesSum = await context.CreditCardPurchases
                .AsNoTracking()
                .Where(p => p.CreditCardId == creditCardId)
                .SumAsync(p => (decimal?)p.Amount, ct)
                .ConfigureAwait(false) ?? 0m;

            var repaymentsQuery = context.Transactions
                .AsNoTracking()
                .Where(t => t.CreditCardId == creditCardId
                            && !t.IsDeleted
                            && t.Type == OperationType.Expense
                            && t.Category.Name == RepaymentCategoryName);

            if (excludeRepaymentTransactionId.HasValue)
                repaymentsQuery = repaymentsQuery.Where(t => t.Id != excludeRepaymentTransactionId.Value);

            var repaymentsSum = await repaymentsQuery
                .SumAsync(t => (decimal?)t.Amount, ct)
                .ConfigureAwait(false) ?? 0m;

            // Пополнения карты (операции дохода, привязанные к карте) уменьшают долг
            // наравне с погашениями: деньги пришли на карту, но не из кассы.
            var replenishmentsSum = await context.Transactions
                .AsNoTracking()
                .Where(t => t.CreditCardId == creditCardId
                            && !t.IsDeleted
                            && t.Type == OperationType.Income)
                .SumAsync(t => (decimal?)t.Amount, ct)
                .ConfigureAwait(false) ?? 0m;

            return card.InitialDebt + purchasesSum - repaymentsSum - replenishmentsSum;
        }

        /// <summary>
        /// Зарегистрировать покупку по кредитной карте в переданном контексте.
        /// Проверяет кредитный лимит до добавления записи.
        /// </summary>
        public static async Task AddPurchaseAsync(
            AppDbContext context,
            int creditCardId,
            int transactionId,
            decimal amount,
            CancellationToken ct = default)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма покупки должна быть больше нуля", nameof(amount));

            var card = await context.CreditCards
                .FindAsync(new object[] { creditCardId }, ct)
                .ConfigureAwait(false);

            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={creditCardId} не найдена");

            if (!card.IsActive)
                throw new InvalidOperationException("Нельзя добавить покупку по неактивной карте");

            var transaction = await context.Transactions
                .FindAsync(new object[] { transactionId }, ct)
                .ConfigureAwait(false);

            if (transaction == null)
                throw new InvalidOperationException($"Операция ID={transactionId} не найдена");

            var currentDebt = await GetCurrentDebtAsync(context, creditCardId, ct: ct).ConfigureAwait(false);
            if (currentDebt + amount > card.Limit)
                throw new InvalidOperationException("Покупка превышает кредитный лимит карты");

            context.CreditCardPurchases.Add(new CreditCardPurchase
            {
                CreditCardId = creditCardId,
                TransactionId = transactionId,
                Amount = amount,
                RemainingDebt = amount,
                PurchaseDate = transaction.Date,
                Notes = transaction.Description
            });
        }

        /// <summary>
        /// Распределить сумму погашения кредитной карты по покупкам FIFO
        /// в рамках переданного контекста.
        /// </summary>
        public static async Task DistributeRepaymentAsync(
            AppDbContext context,
            int transactionId,
            int creditCardId,
            decimal amount,
            CancellationToken ct = default)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма погашения должна быть больше нуля", nameof(amount));

            var transaction = await context.Transactions
                .FindAsync(new object[] { transactionId }, ct)
                .ConfigureAwait(false);

            if (transaction == null)
                throw new InvalidOperationException($"Операция ID={transactionId} не найдена");

            var card = await context.CreditCards
                .FindAsync(new object[] { creditCardId }, ct)
                .ConfigureAwait(false);

            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={creditCardId} не найдена");

            card.LastPaymentDate = DateTime.Today;

            var remainingAmount = amount;

            var purchases = await context.CreditCardPurchases
                .Where(p => p.CreditCardId == creditCardId && p.RemainingDebt > 0)
                .OrderBy(p => p.PurchaseDate)
                .ThenBy(p => p.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var purchase in purchases)
            {
                if (remainingAmount <= 0)
                    break;

                var paymentAmount = Math.Min(purchase.RemainingDebt, remainingAmount);
                purchase.RemainingDebt -= paymentAmount;
                remainingAmount -= paymentAmount;

                context.CreditCardPayments.Add(new CreditCardPayment
                {
                    RepaymentTransactionId = transactionId,
                    CreditCardPurchaseId = purchase.Id,
                    Amount = paymentAmount
                });
            }
        }

        /// <summary>
        /// Восстановить оставшиеся долги покупок при удалении операции-погашения.
        /// Удаляет записи <see cref="CreditCardPayment"/> из переданного контекста.
        /// </summary>
        public static async Task RecalculateOnRepaymentDeletedAsync(
            AppDbContext context,
            int repaymentTransactionId,
            CancellationToken ct = default)
        {
            var creditCardPayments = await context.CreditCardPayments
                .Where(ccp => ccp.RepaymentTransactionId == repaymentTransactionId)
                .Include(ccp => ccp.CreditCardPurchase)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var creditCardPayment in creditCardPayments)
            {
                if (creditCardPayment.CreditCardPurchase != null)
                    creditCardPayment.CreditCardPurchase.RemainingDebt += creditCardPayment.Amount;
            }

            context.CreditCardPayments.RemoveRange(creditCardPayments);
        }
    }
}
