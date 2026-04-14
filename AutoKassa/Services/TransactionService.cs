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

        private readonly AppDbContext _context;

        public TransactionService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Получить список операций с фильтрацией
        /// </summary>
        public async Task<List<Transaction>> GetTransactionsAsync(TransactionFilterParameters filters, CancellationToken ct = default)
        {
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => !t.IsDeleted);

            query = ApplyFilters(query, filters);
            query = ApplySorting(query, filters.SortBy, filters.SortDescending);

            // SQLite LOWER() не поддерживает Unicode (кириллицу), поэтому
            // поиск по описанию выполняется в памяти, после SQL-фильтрации.
            if (!string.IsNullOrWhiteSpace(filters.SearchText))
            {
                var all = await query.ToListAsync(ct);
                return all
                    .Where(t => t.Description != null &&
                                t.Description.Contains(filters.SearchText, StringComparison.OrdinalIgnoreCase))
                    .Skip(filters.Skip)
                    .Take(filters.Take)
                    .ToList();
            }

            query = query.Skip(filters.Skip).Take(filters.Take);
            return await query.ToListAsync(ct);
        }

        /// <summary>
        /// Получить общее количество операций с учетом фильтров
        /// </summary>
        public async Task<int> GetTotalCountAsync(TransactionFilterParameters filters, CancellationToken ct = default)
        {
            var query = _context.Transactions
                .Where(t => !t.IsDeleted);

            query = ApplyFilters(query, filters);

            if (!string.IsNullOrWhiteSpace(filters.SearchText))
            {
                var all = await query.Select(t => t.Description).ToListAsync(ct);
                return all.Count(d => d != null &&
                                      d.Contains(filters.SearchText, StringComparison.OrdinalIgnoreCase));
            }

            return await query.CountAsync(ct);
        }

        /// <summary>
        /// Получить операцию по ID
        /// </summary>
        public async Task<Transaction> GetByIdAsync(int id)
        {
            return await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        }

        /// <summary>
        /// Добавить новую операцию
        /// </summary>
        public async Task<Transaction> AddAsync(Transaction transaction)
        {
            if (transaction.Amount <= 0)
                throw new ArgumentException("Сумма операции должна быть больше нуля", nameof(transaction));

            var categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == transaction.CategoryId && c.IsActive);
            if (!categoryExists)
                throw new InvalidOperationException($"Категория ID={transaction.CategoryId} не найдена или неактивна");

            transaction.CreatedAt = DateTime.Now;
            transaction.IsDeleted = false;

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            // Загружаем категорию
            await _context.Entry(transaction).Reference(t => t.Category).LoadAsync();

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

            var categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == transaction.CategoryId && c.IsActive);
            if (!categoryExists)
                throw new InvalidOperationException($"Категория ID={transaction.CategoryId} не найдена или неактивна");

            transaction.UpdatedAt = DateTime.Now;

            _context.Entry(transaction).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Обновляем категорию
            await _context.Entry(transaction).Reference(t => t.Category).LoadAsync();

            _log.Information("Обновлена операция ID={Id}, сумма={Amount}", transaction.Id, transaction.Amount);
        }

        /// <summary>
        /// Удалить операцию (soft delete)
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                transaction.IsDeleted = true;
                transaction.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                _log.Information("Удалена операция ID={Id}", id);
            }
        }

        /// <summary>
        /// Восстановить ранее удалённую операцию (отмена soft delete)
        /// </summary>
        public async Task RestoreAsync(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                transaction.IsDeleted = false;
                transaction.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                _log.Information("Восстановлена операция ID={Id}", id);
            }
        }

        /// <summary>
        /// Получить последние N операций
        /// </summary>
        public async Task<List<Transaction>> GetRecentAsync(int count = 10)
        {
            return await _context.Transactions
                .Include(t => t.Category)
                .Where(t => !t.IsDeleted)
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        /// <summary>
        /// Суммарные доходы/расходы за период — SQL GROUP BY, без загрузки всех записей
        /// </summary>
        public async Task<(decimal Income, decimal Expense, int IncomeCount, int ExpenseCount)> GetPeriodTotalsAsync(DateTime from, DateTime to, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            var dateTo = to.Date.AddDays(1);
            var query = _context.Transactions
                .Where(t => !t.IsDeleted && t.Date >= from && t.Date < dateTo);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var rows = await query
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount), Count = g.Count() })
                .ToListAsync(ct);

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
            var dateTo = to.Date.AddDays(1);
            var query = _context.Transactions
                .Where(t => !t.IsDeleted && t.Date >= from && t.Date < dateTo);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var rows = await query
                .GroupBy(t => t.Date.Date)
                .Select(g => new
                {
                    Date    = g.Key,
                    Income  = (decimal)g.Where(t => t.Type == OperationType.Income).Sum(t => (double)t.Amount),
                    Expense = (decimal)g.Where(t => t.Type == OperationType.Expense).Sum(t => (double)t.Amount)
                })
                .OrderBy(x => x.Date)
                .ToListAsync(ct);

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
            var dateTo = to.Date.AddDays(1);
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => !t.IsDeleted && t.Type == type && t.Date >= from && t.Date < dateTo);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var rows = await query
                .GroupBy(t => t.Category.Name ?? "?")
                .Select(g => new { Name = g.Key, Total = g.Sum(t => (double)t.Amount) })
                .OrderByDescending(x => x.Total)
                .Take(count)
                .ToListAsync(ct);

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

            // Поиск по описанию выполняется в памяти (см. GetTransactionsAsync/GetTotalCountAsync):
            // SQLite LOWER() не поддерживает Unicode, поэтому фильтрация по SearchText здесь пропущена.

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

        #endregion
    }
}