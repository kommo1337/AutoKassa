using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с кредитными картами
    /// </summary>
    public class CreditCardService : ICreditCardService
    {
        private static readonly ILogger _log = Log.ForContext<CreditCardService>();

        /// <summary>
        /// Название системной категории расходов, используемой для погашения кредита
        /// </summary>
        public const string RepaymentCategoryName = CreditCardOperationHelper.RepaymentCategoryName;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CreditCardService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Получить список всех кредитных карт
        /// </summary>
        public async Task<IReadOnlyList<CreditCard>> GetAllAsync(CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.CreditCards
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Получить кредитную карту по ID
        /// </summary>
        public async Task<CreditCard?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.CreditCards
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Получить кредитную карту с историей покупок
        /// </summary>
        public async Task<CreditCard?> GetCardWithPurchasesAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.CreditCards
                .AsNoTracking()
                .Include(c => c.Purchases)
                .ThenInclude(p => p.Transaction)
                .ThenInclude(t => t!.Category)
                .FirstOrDefaultAsync(c => c.Id == id, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Создать новую кредитную карту
        /// </summary>
        public async Task<CreditCard> CreateAsync(CreditCard card, CancellationToken ct = default)
        {
            ValidateCard(card);

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            card.CreatedAt = DateTime.Now;
            card.IsActive = true;

            context.CreditCards.Add(card);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information("Создана кредитная карта ID={Id}, название={Name}, лимит={Limit}",
                card.Id, card.Name, card.Limit);

            return card;
        }

        /// <summary>
        /// Обновить кредитную карту
        /// </summary>
        public async Task UpdateAsync(CreditCard card, CancellationToken ct = default)
        {
            ValidateCard(card);

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var existing = await context.CreditCards.FindAsync(new object[] { card.Id }, ct).ConfigureAwait(false);
            if (existing == null)
                throw new InvalidOperationException($"Кредитная карта ID={card.Id} не найдена");

            existing.Name = card.Name;
            existing.BankName = card.BankName;
            existing.Limit = card.Limit;
            existing.InterestRate = card.InterestRate;
            existing.StatementDay = card.StatementDay;
            existing.PaymentDay = card.PaymentDay;
            existing.MinimumPaymentPercent = card.MinimumPaymentPercent;
            existing.InitialDebt = card.InitialDebt;
            existing.IsActive = card.IsActive;

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information("Обновлена кредитная карта ID={Id}, название={Name}", card.Id, card.Name);
        }

        /// <summary>
        /// Деактивировать кредитную карту
        /// </summary>
        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var card = await context.CreditCards.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
            if (card == null)
                return;

            var debt = await GetCurrentDebtAsync(id, ct).ConfigureAwait(false);
            if (debt > 0)
                throw new InvalidOperationException("Нельзя удалить карту с непогашенным долгом");

            card.IsActive = false;
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information("Деактивирована кредитная карта ID={Id}", id);
        }

        /// <summary>
        /// Получить текущий долг по карте
        /// </summary>
        public async Task<decimal> GetCurrentDebtAsync(int creditCardId, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await CreditCardOperationHelper.GetCurrentDebtAsync(context, creditCardId, ct: ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Получить доступный остаток кредитного лимита
        /// </summary>
        public async Task<decimal> GetAvailableLimitAsync(int creditCardId, CancellationToken ct = default)
        {
            var card = await GetByIdAsync(creditCardId, ct).ConfigureAwait(false);
            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={creditCardId} не найдена");

            var debt = await GetCurrentDebtAsync(creditCardId, ct).ConfigureAwait(false);
            return Math.Max(0m, card.Limit - debt);
        }

        /// <summary>
        /// Получить сумму минимального платежа
        /// </summary>
        public async Task<decimal> GetMinimumPaymentAsync(int creditCardId, CancellationToken ct = default)
        {
            var card = await GetByIdAsync(creditCardId, ct).ConfigureAwait(false);
            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={creditCardId} не найдена");

            var debt = await GetCurrentDebtAsync(creditCardId, ct).ConfigureAwait(false);
            return debt * (card.MinimumPaymentPercent / 100m);
        }

        /// <summary>
        /// Получить дату ближайшего платежа
        /// </summary>
        public async Task<DateTime?> GetNextPaymentDateAsync(int creditCardId, CancellationToken ct = default)
        {
            var card = await GetByIdAsync(creditCardId, ct).ConfigureAwait(false);
            if (card == null)
                return null;

            return CalculateNextPaymentDate(card);
        }

        /// <summary>
        /// Получить метрики по всем картам одним пакетом.
        /// Три запроса независимо от количества карт — взамен нескольких контекстов на карту (N+1).
        /// </summary>
        public async Task<IReadOnlyList<CreditCardMetrics>> GetCardsMetricsAsync(CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

            var cards = await context.CreditCards
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var cardIds = cards.Select(c => c.Id).ToList();

            // Суммы покупок по каждой карте
            var purchaseTotals = await context.CreditCardPurchases
                .AsNoTracking()
                .Where(p => cardIds.Contains(p.CreditCardId))
                .GroupBy(p => p.CreditCardId)
                .Select(g => new { CardId = g.Key, Total = g.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.CardId, x => x.Total, ct)
                .ConfigureAwait(false);

            // Суммы погашений по каждой карте (та же логика, что и в CreditCardOperationHelper.GetCurrentDebtAsync)
            var repaymentTotals = await context.Transactions
                .AsNoTracking()
                .Where(t => t.CreditCardId != null
                            && cardIds.Contains(t.CreditCardId.Value)
                            && !t.IsDeleted
                            && t.Type == OperationType.Expense
                            && t.Category.Name == RepaymentCategoryName)
                .GroupBy(t => t.CreditCardId!.Value)
                .Select(g => new { CardId = g.Key, Total = g.Sum(t => t.Amount) })
                .ToDictionaryAsync(x => x.CardId, x => x.Total, ct)
                .ConfigureAwait(false);

            return cards.Select(card =>
            {
                var debt = card.InitialDebt
                           + (purchaseTotals.TryGetValue(card.Id, out var purchases) ? purchases : 0m)
                           - (repaymentTotals.TryGetValue(card.Id, out var repayments) ? repayments : 0m);

                return new CreditCardMetrics
                {
                    Card = card,
                    CurrentDebt = debt,
                    AvailableLimit = Math.Max(0m, card.Limit - debt),
                    MinimumPayment = debt * (card.MinimumPaymentPercent / 100m),
                    NextPaymentDate = CalculateNextPaymentDate(card)
                };
            }).ToList();
        }

        /// <summary>
        /// Рассчитать дату ближайшего платежа по карте
        /// </summary>
        private static DateTime? CalculateNextPaymentDate(CreditCard card)
        {
            if (!card.PaymentDay.HasValue)
                return null;

            var referenceDate = card.LastPaymentDate.HasValue
                ? card.LastPaymentDate.Value.AddMonths(1)
                : DateTime.Today;

            var daysInMonth = DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month);
            var paymentDay = Math.Min(card.PaymentDay.Value, daysInMonth);

            var candidate = new DateTime(referenceDate.Year, referenceDate.Month, paymentDay);

            if (candidate <= DateTime.Today)
            {
                referenceDate = candidate.AddMonths(1);
                daysInMonth = DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month);
                paymentDay = Math.Min(card.PaymentDay.Value, daysInMonth);
                candidate = new DateTime(referenceDate.Year, referenceDate.Month, paymentDay);
            }

            return candidate;
        }

        /// <summary>
        /// Получить общий текущий долг по всем активным картам
        /// </summary>
        public async Task<decimal> GetTotalDebtAsync(CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var cards = await context.CreditCards
                .AsNoTracking()
                .Where(c => c.IsActive)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var cardIds = cards.Select(c => c.Id).ToList();
            var totalInitialDebt = cards.Sum(c => c.InitialDebt);

            var purchasesSum = await context.CreditCardPurchases
                .AsNoTracking()
                .Where(p => cardIds.Contains(p.CreditCardId))
                .SumAsync(p => (decimal?)p.Amount, ct)
                .ConfigureAwait(false) ?? 0m;

            var repaymentsSum = await context.Transactions
                .AsNoTracking()
                .Where(t => t.CreditCardId != null
                            && cardIds.Contains(t.CreditCardId.Value)
                            && !t.IsDeleted
                            && t.Type == OperationType.Expense
                            && t.Category.Name == RepaymentCategoryName)
                .SumAsync(t => (decimal?)t.Amount, ct)
                .ConfigureAwait(false) ?? 0m;

            return totalInitialDebt + purchasesSum - repaymentsSum;
        }

        /// <summary>
        /// Получить общий долг по всем активным картам на конец указанной даты
        /// </summary>
        public async Task<decimal> GetTotalDebtAsOfAsync(DateTime asOfDate, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            // Верхняя граница — конец указанной даты (включительно)
            var asOfEnd = asOfDate.Date.AddDays(1);

            var cards = await context.CreditCards
                .AsNoTracking()
                .Where(c => c.IsActive)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var cardIds = cards.Select(c => c.Id).ToList();
            var totalInitialDebt = cards.Sum(c => c.InitialDebt);

            var purchasesSum = await context.CreditCardPurchases
                .AsNoTracking()
                .Where(p => cardIds.Contains(p.CreditCardId) && p.PurchaseDate < asOfEnd)
                .SumAsync(p => (decimal?)p.Amount, ct)
                .ConfigureAwait(false) ?? 0m;

            // Фильтры погашений повторяют GetTotalDebtAsync и CreditCardOperationHelper.GetCurrentDebtAsync
            var repaymentsSum = await context.Transactions
                .AsNoTracking()
                .Where(t => t.CreditCardId != null
                            && cardIds.Contains(t.CreditCardId.Value)
                            && !t.IsDeleted
                            && t.Type == OperationType.Expense
                            && t.Category.Name == RepaymentCategoryName
                            && t.Date < asOfEnd)
                .SumAsync(t => (decimal?)t.Amount, ct)
                .ConfigureAwait(false) ?? 0m;

            return totalInitialDebt + purchasesSum - repaymentsSum;
        }

        /// <summary>
        /// Зарегистрировать покупку по кредитной карте
        /// </summary>
        public async Task AddPurchaseAsync(int creditCardId, int transactionId, decimal amount, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            await CreditCardOperationHelper.AddPurchaseAsync(
                context, creditCardId, transactionId, amount, ct).ConfigureAwait(false);

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information("Добавлена кредитная покупка по карте ID={CreditCardId}, операция ID={TransactionId}, сумма={Amount}",
                creditCardId, transactionId, amount);
        }

        /// <summary>
        /// Создать операцию-погашение долга по кредитной карте и распределить сумму по покупкам FIFO
        /// </summary>
        public async Task<Transaction> RepayDebtAsync(
            int creditCardId,
            decimal amount,
            PaymentType paymentType,
            DateTime date,
            string? description = null,
            CancellationToken ct = default)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма погашения должна быть больше нуля", nameof(amount));

            if (paymentType != PaymentType.Cash && paymentType != PaymentType.NonCash)
                throw new ArgumentException("Погашение кредитной карты возможно только наличными или безналичными", nameof(paymentType));

            // Соответствует [MaxLength(500)] в Models/Transaction.cs
            if (description?.Length > 500)
                throw new ArgumentException("Описание не должно превышать 500 символов", nameof(description));

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var card = await context.CreditCards.FindAsync(new object[] { creditCardId }, ct).ConfigureAwait(false);
            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={creditCardId} не найдена");

            if (!card.IsActive)
                throw new InvalidOperationException("Нельзя погасить неактивную карту");

            var repaymentCategory = await context.Categories
                .FirstOrDefaultAsync(c => c.Name == RepaymentCategoryName && c.Type == OperationType.Expense, ct)
                .ConfigureAwait(false);
            if (repaymentCategory == null)
                throw new InvalidOperationException($"Системная категория \"{RepaymentCategoryName}\" не найдена");

            var currentDebt = await CreditCardOperationHelper.GetCurrentDebtAsync(context, creditCardId, ct: ct).ConfigureAwait(false);
            if (amount > currentDebt)
                throw new InvalidOperationException("Сумма погашения превышает текущий долг по карте");

            var transaction = new Transaction
            {
                Date = date,
                Amount = amount,
                Type = OperationType.Expense,
                PaymentType = paymentType,
                CategoryId = repaymentCategory.Id,
                CreditCardId = creditCardId,
                Description = description,
                CreatedAt = DateTime.Now,
                IsDeleted = false,
                DebtStatus = DebtStatus.NotDebt
            };

            context.Transactions.Add(transaction);

            // Сначала сохраняем операцию, чтобы получить сгенерированный Id для связей
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            card.LastPaymentDate = DateTime.Today;

            await CreditCardOperationHelper.DistributeRepaymentAsync(
                context, transaction.Id, creditCardId, amount, ct).ConfigureAwait(false);

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information(
                "Создано погашение кредитной карты ID={CreditCardId}, операция ID={TransactionId}, сумма={Amount}, оставшийся долг={Debt}",
                creditCardId, transaction.Id, amount, currentDebt - amount);

            return transaction;
        }

        /// <summary>
        /// Получить покупки карты с ненулевым остатком долга, отсортированные по FIFO
        /// </summary>
        public async Task<IReadOnlyList<CreditCardPurchase>> GetPurchasesWithRemainingDebtAsync(
            int creditCardId,
            CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            return await context.CreditCardPurchases
                .AsNoTracking()
                .Where(p => p.CreditCardId == creditCardId && p.RemainingDebt > 0)
                .OrderBy(p => p.PurchaseDate)
                .ThenBy(p => p.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Получить текущий оставшийся долг по покупке
        /// </summary>
        public async Task<decimal> GetRemainingDebtForPurchaseAsync(int purchaseId, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var purchase = await context.CreditCardPurchases
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == purchaseId, ct)
                .ConfigureAwait(false);

            if (purchase == null)
                throw new InvalidOperationException($"Покупка ID={purchaseId} не найдена");

            return purchase.RemainingDebt;
        }

        /// <summary>
        /// Распределить сумму существующей операции-погашения по покупкам FIFO
        /// </summary>
        public async Task DistributeRepaymentForTransactionAsync(
            int transactionId,
            int creditCardId,
            decimal amount,
            CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            await CreditCardOperationHelper.DistributeRepaymentAsync(
                context, transactionId, creditCardId, amount, ct).ConfigureAwait(false);

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information(
                "Распределено погашение операции ID={TransactionId} по карте ID={CreditCardId}, сумма={Amount}",
                transactionId, creditCardId, amount);
        }

        /// <summary>
        /// Восстановить оставшиеся долги покупок при удалении операции-погашения
        /// </summary>
        public async Task RecalculateOnRepaymentDeletedAsync(int repaymentTransactionId, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            await CreditCardOperationHelper.RecalculateOnRepaymentDeletedAsync(
                context, repaymentTransactionId, ct).ConfigureAwait(false);

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information(
                "Удалено погашение операции ID={RepaymentTransactionId}",
                repaymentTransactionId);
        }

        /// <summary>
        /// Валидация данных карты
        /// </summary>
        private static void ValidateCard(CreditCard card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));

            if (string.IsNullOrWhiteSpace(card.Name))
                throw new ArgumentException("Название карты не может быть пустым", nameof(card));

            // Лимиты соответствуют [MaxLength(100)] в Models/CreditCard.cs
            if (card.Name.Length > 100)
                throw new ArgumentException("Название карты не должно превышать 100 символов", nameof(card));

            if (card.BankName?.Length > 100)
                throw new ArgumentException("Название банка не должно превышать 100 символов", nameof(card));

            if (card.Limit < 0)
                throw new ArgumentException("Кредитный лимит не может быть отрицательным", nameof(card));

            if (card.InitialDebt < 0)
                throw new ArgumentException("Начальный долг не может быть отрицательным", nameof(card));

            if (card.MinimumPaymentPercent < 0 || card.MinimumPaymentPercent > 100)
                throw new ArgumentException("Процент минимального платежа должен быть в диапазоне 0–100", nameof(card));

            if (card.StatementDay.HasValue && (card.StatementDay.Value < 1 || card.StatementDay.Value > 31))
                throw new ArgumentException("День выписки должен быть в диапазоне 1–31", nameof(card));

            if (card.PaymentDay.HasValue && (card.PaymentDay.Value < 1 || card.PaymentDay.Value > 31))
                throw new ArgumentException("День платежа должен быть в диапазоне 1–31", nameof(card));
        }
    }
}
