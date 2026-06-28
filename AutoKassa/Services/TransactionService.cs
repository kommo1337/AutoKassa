using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с финансовыми операциями
    /// </summary>
    public class TransactionService : ITransactionService
    {
        private static readonly ILogger _log = Log.ForContext<TransactionService>();

        /// <summary>
        /// Максимальное количество записей, загружаемых в память для текстового поиска.
        /// Защита от RAM-всплеска при большом количестве операций.
        /// </summary>
        private const int MaxInMemoryFilterLimit = 5000;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ICreditCardService _creditCardService;
        private readonly IDebtService _debtService;

        public TransactionService(
            IDbContextFactory<AppDbContext> contextFactory,
            ICreditCardService creditCardService,
            IDebtService debtService)
        {
            _contextFactory = contextFactory;
            _creditCardService = creditCardService;
            _debtService = debtService;
        }

        /// <summary>
        /// Получить список операций с фильтрацией
        /// </summary>
        public async Task<List<Transaction>> GetTransactionsAsync(TransactionFilterParameters filters, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var query = context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.CreditCard)
                .Where(t => !t.IsDeleted);

            query = ApplyFilters(query, filters);
            query = ApplySorting(query, filters.SortBy, filters.SortDescending);

            // SQLite LOWER() не поддерживает Unicode (кириллицу), поэтому
            // поиск по описанию выполняется в памяти, после SQL-фильтрации.
            // Для защиты от RAM-всплеска на больших таблицах ограничиваем выборку.
            if (!string.IsNullOrWhiteSpace(filters.SearchText))
            {
                var candidates = await query.Take(MaxInMemoryFilterLimit).ToListAsync(ct).ConfigureAwait(false);
                var filtered = candidates
                    .Where(t => t.Description != null &&
                                t.Description.Contains(filters.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return filtered.Skip(filters.Skip).Take(filters.Take).ToList();
            }

            query = query.Skip(filters.Skip).Take(filters.Take);
            return await query.ToListAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Получить общее количество операций с учетом фильтров
        /// </summary>
        public async Task<int> GetTotalCountAsync(TransactionFilterParameters filters, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var query = context.Transactions
                .AsNoTracking()
                .Where(t => !t.IsDeleted);

            query = ApplyFilters(query, filters);

            if (!string.IsNullOrWhiteSpace(filters.SearchText))
            {
                var descriptions = await query.Select(t => t.Description).Take(MaxInMemoryFilterLimit).ToListAsync(ct).ConfigureAwait(false);
                var count = descriptions.Count(d => d != null &&
                                                    d.Contains(filters.SearchText, StringComparison.OrdinalIgnoreCase));
                // Если достигли лимита, возвращаем лимит как приближённое значение.
                return count >= MaxInMemoryFilterLimit ? MaxInMemoryFilterLimit : count;
            }

            return await query.CountAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Получить операцию по ID (tracked, для редактирования)
        /// </summary>
        public async Task<Transaction> GetByIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.Transactions
                .Include(t => t.Category)
                .Include(t => t.CreditCard)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Добавить новую операцию
        /// </summary>
        public async Task<Transaction> AddAsync(Transaction transaction)
        {
            if (transaction.Amount <= 0)
                throw new ArgumentException("Сумма операции должна быть больше нуля", nameof(transaction));

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var categoryExists = await context.Categories
                .AnyAsync(c => c.Id == transaction.CategoryId && c.IsActive)
                .ConfigureAwait(false);
            if (!categoryExists)
                throw new InvalidOperationException($"Категория ID={transaction.CategoryId} не найдена или неактивна");

            transaction.CreatedAt = DateTime.Now;
            transaction.IsDeleted = false;

            // Обработка долговой операции
            if (transaction.PaymentType == PaymentType.Debt)
            {
                if (!transaction.CounterpartyId.HasValue)
                    throw new InvalidOperationException("Для долговой операции необходимо выбрать контрагента");

                transaction.DebtStatus = DebtStatus.Active;
            }
            else
            {
                transaction.DebtStatus = DebtStatus.NotDebt;
            }

            context.Transactions.Add(transaction);
            await context.SaveChangesAsync().ConfigureAwait(false);

            // Загружаем категорию
            var category = await context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == transaction.CategoryId)
                .ConfigureAwait(false);
            if (category != null)
                transaction.Category = category;

            var isRepayment = category?.Name == CreditCardService.RepaymentCategoryName;

            // Обработка операций по кредитной карте
            if (transaction.PaymentType == PaymentType.CreditCard && transaction.CreditCardId.HasValue)
            {
                await _creditCardService.AddPurchaseAsync(
                    transaction.CreditCardId.Value,
                    transaction.Id,
                    transaction.Amount,
                    CancellationToken.None).ConfigureAwait(false);
            }
            else if (isRepayment && transaction.CreditCardId.HasValue)
            {
                await _creditCardService.RepayDebtAsync(
                    transaction.CreditCardId.Value,
                    transaction.Amount,
                    CancellationToken.None).ConfigureAwait(false);
            }

            _log.Information("Добавлена операция ID={Id}, тип={Type}, сумма={Amount}", transaction.Id, transaction.Type, transaction.Amount);
            return transaction;
        }

        /// <summary>
        /// Обновить операцию
        /// </summary>
        public async Task UpdateAsync(Transaction transaction)
        {
            if (transaction.Amount <= 0)
                throw new ArgumentException("Сумма операции должна быть больше нуля", nameof(transaction));

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var categoryExists = await context.Categories
                .AnyAsync(c => c.Id == transaction.CategoryId && c.IsActive)
                .ConfigureAwait(false);
            if (!categoryExists)
                throw new InvalidOperationException($"Категория ID={transaction.CategoryId} не найдена или неактивна");

            // Каждый вызов использует свой DbContext (unit of work), поэтому конфликтов
            // отслеживания быть не может. Загружаем сущность явно и обновляем поля.
            var existing = await context.Transactions.FindAsync(transaction.Id).ConfigureAwait(false);
            if (existing == null)
                throw new InvalidOperationException($"Операция ID={transaction.Id} не найдена");

            var isRepayment = await context.DebtPayments
                .AnyAsync(dp => dp.RepaymentTransactionId == transaction.Id)
                .ConfigureAwait(false);
            if (isRepayment)
                throw new InvalidOperationException("Редактирование операции-погашения запрещено");

            var oldPaymentType = existing.PaymentType;
            var oldCreditCardId = existing.CreditCardId;
            var oldCategoryId = existing.CategoryId;
            var oldAmount = existing.Amount;

            existing.Date = transaction.Date;
            existing.Amount = transaction.Amount;
            existing.Type = transaction.Type;
            existing.PaymentType = transaction.PaymentType;
            existing.CategoryId = transaction.CategoryId;
            existing.CreditCardId = transaction.CreditCardId;
            existing.CounterpartyId = transaction.CounterpartyId;
            existing.Description = transaction.Description;
            existing.UpdatedAt = DateTime.Now;

            // Обработка долговой операции
            if (existing.PaymentType == PaymentType.Debt)
            {
                if (!existing.CounterpartyId.HasValue)
                    throw new InvalidOperationException("Для долговой операции необходимо выбрать контрагента");

                // Если операция ранее не была долгом — устанавливаем статус Active
                if (existing.DebtStatus == DebtStatus.NotDebt)
                    existing.DebtStatus = DebtStatus.Active;
            }
            else
            {
                existing.DebtStatus = DebtStatus.NotDebt;
                existing.CounterpartyId = null;

                // При смене типа операции с долга на обычную удаляем связи с погашениями
                var debtPayments = await context.DebtPayments
                    .Where(dp => dp.DebtTransactionId == existing.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);
                if (debtPayments.Count > 0)
                    context.DebtPayments.RemoveRange(debtPayments);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            // Обновляем навигационное свойство
            await context.Entry(existing).Reference(t => t.Category).LoadAsync().ConfigureAwait(false);

            // Обработка изменений, связанных с кредитной картой
            await HandleCreditCardChangesAsync(
                context,
                existing,
                oldPaymentType,
                oldCreditCardId,
                oldCategoryId,
                oldAmount).ConfigureAwait(false);

            _log.Information("Обновлена операция ID={Id}, сумма={Amount}", transaction.Id, transaction.Amount);
        }

        /// <summary>
        /// Удалить операцию (soft delete)
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var transaction = await context.Transactions.FindAsync(id).ConfigureAwait(false);
            if (transaction != null)
            {
                transaction.IsDeleted = true;
                transaction.UpdatedAt = DateTime.Now;

                // При удалении операции по кредитной карте удаляем связанную покупку
                if (transaction.PaymentType == PaymentType.CreditCard)
                {
                    var purchase = await context.CreditCardPurchases
                        .FirstOrDefaultAsync(p => p.TransactionId == id)
                        .ConfigureAwait(false);
                    if (purchase != null)
                    {
                        context.CreditCardPurchases.Remove(purchase);
                    }
                }

                int? relatedDebtId = null;

                // При удалении долговой операции удаляем связанные записи DebtPayment
                // и делаем soft delete операций-погашений
                if (transaction.PaymentType == PaymentType.Debt)
                {
                    var debtPayments = await context.DebtPayments
                        .Where(dp => dp.DebtTransactionId == id)
                        .Include(dp => dp.RepaymentTransaction)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    foreach (var debtPayment in debtPayments)
                    {
                        if (debtPayment.RepaymentTransaction != null)
                        {
                            debtPayment.RepaymentTransaction.IsDeleted = true;
                            debtPayment.RepaymentTransaction.UpdatedAt = DateTime.Now;
                        }
                    }

                    if (debtPayments.Count > 0)
                    {
                        context.DebtPayments.RemoveRange(debtPayments);
                    }
                }
                else
                {
                    // Проверяем, является ли удаляемая операция погашением долга
                    var debtPayment = await context.DebtPayments
                        .FirstOrDefaultAsync(dp => dp.RepaymentTransactionId == id)
                        .ConfigureAwait(false);

                    if (debtPayment != null)
                    {
                        relatedDebtId = debtPayment.DebtTransactionId;
                        context.DebtPayments.Remove(debtPayment);
                    }
                }

                await context.SaveChangesAsync().ConfigureAwait(false);
                _log.Information("Удалена операция ID={Id}", id);

                // Если удалили погашение — пересчитываем статус связанного долга
                if (relatedDebtId.HasValue)
                {
                    await _debtService.RecalculateStatusAsync(relatedDebtId.Value).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Восстановить ранее удалённую операцию (отмена soft delete)
        /// </summary>
        public async Task RestoreAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var transaction = await context.Transactions.FindAsync(id).ConfigureAwait(false);
            if (transaction != null)
            {
                transaction.IsDeleted = false;
                transaction.UpdatedAt = DateTime.Now;
                await context.SaveChangesAsync().ConfigureAwait(false);
                _log.Information("Восстановлена операция ID={Id}", id);
            }
        }

        /// <summary>
        /// Проверяет, является ли операция погашением долга.
        /// </summary>
        public async Task<bool> IsRepaymentTransactionAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.DebtPayments
                .AnyAsync(dp => dp.RepaymentTransactionId == id, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Получить последние N операций
        /// </summary>
        public async Task<List<Transaction>> GetRecentAsync(int count = 10)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.CreditCard)
                .Where(t => !t.IsDeleted)
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.CreatedAt)
                .Take(count)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Суммарные доходы/расходы за период — SQL GROUP BY, без загрузки всех записей
        /// </summary>
        public async Task<(decimal Income, decimal Expense, int IncomeCount, int ExpenseCount)> GetPeriodTotalsAsync(DateTime from, DateTime to, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dateTo = to.Date.AddDays(1);
            var query = context.Transactions
                .Where(t => !t.IsDeleted && t.Date >= from && t.Date < dateTo);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var rows = await query
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount), Count = g.Count() })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var incomeRow  = rows.FirstOrDefault(r => r.Type == OperationType.Income);
            var expenseRow = rows.FirstOrDefault(r => r.Type == OperationType.Expense);
            return (
                incomeRow?.Total  ?? 0m,
                expenseRow?.Total ?? 0m,
                incomeRow?.Count  ?? 0,
                expenseRow?.Count ?? 0);
        }

        /// <summary>
        /// Дневные итоги за период — SQL GROUP BY date, для графиков и группировки
        /// </summary>
        public async Task<List<DailyTotalsItem>> GetDailyTotalsAsync(DateTime from, DateTime to, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dateTo = to.Date.AddDays(1);
            var query = context.Transactions
                .Where(t => !t.IsDeleted && t.Date >= from && t.Date < dateTo);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var rows = await query
                .GroupBy(t => t.Date.Date)
                .Select(g => new
                {
                    Date    = g.Key,
                    Income  = (decimal)g.Sum(t => t.Type == OperationType.Income ? (double)t.Amount : 0),
                    Expense = (decimal)g.Sum(t => t.Type == OperationType.Expense ? (double)t.Amount : 0)
                })
                .OrderBy(x => x.Date)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return rows.Select(r => new DailyTotalsItem
            {
                Date    = r.Date,
                Income  = r.Income,
                Expense = r.Expense
            }).ToList();
        }

        /// <summary>
        /// Топ N категорий по сумме для заданного типа операции
        /// </summary>
        public async Task<List<(string Name, decimal Total)>> GetTopCategoriesAsync(
            DateTime from, DateTime to, OperationType type, int count, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dateTo = to.Date.AddDays(1);
            var query = context.Transactions
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Type == type && t.Date >= from && t.Date < dateTo);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var rows = await query
                .GroupBy(t => t.Category.Name ?? "?")
                .Select(g => new { Name = g.Key, Total = g.Sum(t => (double)t.Amount) })
                .OrderByDescending(x => x.Total)
                .Take(count)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return rows.Select(r => (r.Name, (decimal)r.Total)).ToList();
        }

        #region Вспомогательные методы

        /// <summary>
        /// Применить фильтры к запросу
        /// </summary>
        private IQueryable<Transaction> ApplyFilters(IQueryable<Transaction> query, TransactionFilterParameters filters)
        {
            // Фильтр по дате
            if (filters.DateFrom.HasValue)
            {
                query = query.Where(t => t.Date >= filters.DateFrom.Value);
            }

            if (filters.DateTo.HasValue)
            {
                // Exclusive upper bound: включаем весь день DateTo
                var dateTo = filters.DateTo.Value.Date.AddDays(1);
                query = query.Where(t => t.Date < dateTo);
            }

            // Фильтр по типу
            if (filters.Type.HasValue)
            {
                query = query.Where(t => t.Type == filters.Type.Value);
            }

            // Фильтр по типу оплаты
            if (filters.PaymentType.HasValue)
            {
                query = query.Where(t => t.PaymentType == filters.PaymentType.Value);
            }

            // Фильтр по категории
            if (filters.CategoryId.HasValue)
            {
                query = query.Where(t => t.CategoryId == filters.CategoryId.Value);
            }

            // Фильтр по контрагенту
            if (filters.CounterpartyId.HasValue)
            {
                query = query.Where(t => t.CounterpartyId == filters.CounterpartyId.Value);
            }

            // Фильтр по долгам
            if (filters.IsDebtFilter)
            {
                query = query.Where(t => t.PaymentType == PaymentType.Debt && t.DebtStatus != DebtStatus.NotDebt);
            }
            else
            {
                // По умолчанию операции создания долга скрыты из списка операций;
                // отображаются только обычные операции и операции-погашения.
                query = query.Where(t => t.PaymentType != PaymentType.Debt);
            }

            // Фильтр по статусу долга
            if (filters.DebtStatus.HasValue)
            {
                query = query.Where(t => t.DebtStatus == filters.DebtStatus.Value);
            }

            // Фильтр по сумме
            if (filters.AmountFrom.HasValue)
            {
                query = query.Where(t => t.Amount >= filters.AmountFrom.Value);
            }

            if (filters.AmountTo.HasValue)
            {
                query = query.Where(t => t.Amount <= filters.AmountTo.Value);
            }

            return query;
        }

        /// <summary>
        /// Применить сортировку к запросу
        /// </summary>
        private IQueryable<Transaction> ApplySorting(IQueryable<Transaction> query, string sortBy, bool descending)
        {
            switch (sortBy?.ToLower())
            {
                case "date":
                    query = descending
                        ? query.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt)
                        : query.OrderBy(t => t.Date).ThenBy(t => t.CreatedAt);
                    break;

                case "amount":
                    query = descending
                        ? query.OrderByDescending(t => t.Amount)
                        : query.OrderBy(t => t.Amount);
                    break;

                case "type":
                    query = descending
                        ? query.OrderByDescending(t => t.Type)
                        : query.OrderBy(t => t.Type);
                    break;

                case "category":
                    query = descending
                        ? query.OrderByDescending(t => t.Category.Name)
                        : query.OrderBy(t => t.Category.Name);
                    break;

                default:
                    query = query.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt);
                    break;
            }

            return query;
        }

        /// <summary>
        /// Обработать изменения, связанные с кредитной картой, при обновлении операции
        /// </summary>
        private async Task HandleCreditCardChangesAsync(
            AppDbContext context,
            Transaction transaction,
            PaymentType oldPaymentType,
            int? oldCreditCardId,
            int oldCategoryId,
            decimal oldAmount)
        {
            var currentCategory = await context.Categories.FindAsync(transaction.CategoryId).ConfigureAwait(false);
            var oldCategory = await context.Categories.FindAsync(oldCategoryId).ConfigureAwait(false);
            var isRepayment = currentCategory?.Name == CreditCardService.RepaymentCategoryName;
            var wasRepayment = oldCategory?.Name == CreditCardService.RepaymentCategoryName;

            // Удаляем связанную покупку, если тип оплаты перестал быть кредитным
            if (oldPaymentType == PaymentType.CreditCard &&
                (transaction.PaymentType != PaymentType.CreditCard || !transaction.CreditCardId.HasValue))
            {
                var purchase = await context.CreditCardPurchases
                    .FirstOrDefaultAsync(p => p.TransactionId == transaction.Id)
                    .ConfigureAwait(false);
                if (purchase != null)
                {
                    context.CreditCardPurchases.Remove(purchase);
                    await context.SaveChangesAsync().ConfigureAwait(false);
                }
            }

            // Создаём или обновляем покупку, если операция стала/осталась кредитной
            if (transaction.PaymentType == PaymentType.CreditCard && transaction.CreditCardId.HasValue)
            {
                var purchase = await context.CreditCardPurchases
                    .FirstOrDefaultAsync(p => p.TransactionId == transaction.Id)
                    .ConfigureAwait(false);

                if (purchase != null)
                {
                    purchase.CreditCardId = transaction.CreditCardId.Value;
                    purchase.Amount = transaction.Amount;
                    purchase.RemainingDebt = transaction.Amount;
                    purchase.PurchaseDate = transaction.Date;
                    purchase.Notes = transaction.Description;
                    await context.SaveChangesAsync().ConfigureAwait(false);
                }
                else
                {
                    await _creditCardService.AddPurchaseAsync(
                        transaction.CreditCardId.Value,
                        transaction.Id,
                        transaction.Amount,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }

            // Фиксируем погашение, если категория изменилась на "Погашение кредита"
            if (isRepayment && !wasRepayment && transaction.CreditCardId.HasValue)
            {
                await _creditCardService.RepayDebtAsync(
                    transaction.CreditCardId.Value,
                    transaction.Amount,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        #endregion
    }
}
