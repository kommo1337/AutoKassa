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
        public const string RepaymentCategoryName = "Погашение кредита";

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
                .ThenInclude(t => t.Category)
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

            var repaymentsSum = await context.Transactions
                .AsNoTracking()
                .Where(t => t.CreditCardId == creditCardId
                            && !t.IsDeleted
                            && t.Type == OperationType.Expense
                            && t.Category.Name == RepaymentCategoryName)
                .SumAsync(t => (decimal?)t.Amount, ct)
                .ConfigureAwait(false) ?? 0m;

            return card.InitialDebt + purchasesSum - repaymentsSum;
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
            if (card == null || !card.PaymentDay.HasValue)
                return null;

            var referenceDate = card.LastPaymentDate.HasValue
                ? card.LastPaymentDate.Value.AddMonths(1)
                : DateTime.Today;

            var candidate = new DateTime(referenceDate.Year, referenceDate.Month, card.PaymentDay.Value);

            if (candidate <= DateTime.Today)
                candidate = candidate.AddMonths(1);

            return candidate;
        }

        /// <summary>
        /// Зарегистрировать покупку по кредитной карте
        /// </summary>
        public async Task AddPurchaseAsync(int creditCardId, int transactionId, decimal amount, CancellationToken ct = default)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма покупки должна быть больше нуля", nameof(amount));

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var card = await context.CreditCards.FindAsync(new object[] { creditCardId }, ct).ConfigureAwait(false);
            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={creditCardId} не найдена");

            if (!card.IsActive)
                throw new InvalidOperationException("Нельзя добавить покупку по неактивной карте");

            var transaction = await context.Transactions.FindAsync(new object[] { transactionId }, ct).ConfigureAwait(false);
            if (transaction == null)
                throw new InvalidOperationException($"Операция ID={transactionId} не найдена");

            var currentDebt = await GetCurrentDebtAsync(creditCardId, ct).ConfigureAwait(false);
            if (currentDebt + amount > card.Limit)
                throw new InvalidOperationException("Покупка превышает кредитный лимит карты");

            var purchase = new CreditCardPurchase
            {
                CreditCardId = creditCardId,
                TransactionId = transactionId,
                Amount = amount,
                RemainingDebt = amount,
                PurchaseDate = transaction.Date,
                Notes = transaction.Description
            };

            context.CreditCardPurchases.Add(purchase);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information("Добавлена кредитная покупка по карте ID={CreditCardId}, операция ID={TransactionId}, сумма={Amount}",
                creditCardId, transactionId, amount);
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
        /// Зафиксировать погашение долга по кредитной карте
        /// </summary>
        public async Task RepayDebtAsync(int creditCardId, decimal amount, CancellationToken ct = default)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма погашения должна быть больше нуля", nameof(amount));

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var card = await context.CreditCards.FindAsync(new object[] { creditCardId }, ct).ConfigureAwait(false);
            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={creditCardId} не найдена");

            var currentDebt = await GetCurrentDebtAsync(creditCardId, ct).ConfigureAwait(false);
            if (amount > currentDebt)
                throw new InvalidOperationException("Сумма погашения превышает текущий долг по карте");

            card.LastPaymentDate = DateTime.Today;
            var saved = await context.SaveChangesAsync(ct).ConfigureAwait(false);
            if (saved == 0 || !card.LastPaymentDate.HasValue)
                throw new InvalidOperationException($"DEBUG: saved={saved}, LastPaymentDate={card.LastPaymentDate}");

            _log.Information("Погашен долг по карте ID={CreditCardId}, сумма={Amount}, оставшийся долг={Debt}",
                creditCardId, amount, currentDebt - amount);
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
