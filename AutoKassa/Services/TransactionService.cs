using AutoKassa.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с финансовыми операциями
    /// </summary>
    public class TransactionService : ITransactionService
    {
        private readonly AppDbContext _context;

        public TransactionService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Получить список операций с фильтрацией
        /// </summary>
        public async Task<List<Transaction>> GetTransactionsAsync(TransactionFilterParameters filters)
        {
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => !t.IsDeleted); // Только неудаленные

            // Применяем фильтры
            query = ApplyFilters(query, filters);

            // Сортировка
            query = ApplySorting(query, filters.SortBy, filters.SortDescending);

            // Пагинация
            query = query.Skip(filters.Skip).Take(filters.Take);

            return await query.ToListAsync();
        }

        /// <summary>
        /// Получить общее количество операций с учетом фильтров
        /// </summary>
        public async Task<int> GetTotalCountAsync(TransactionFilterParameters filters)
        {
            var query = _context.Transactions
                .Where(t => !t.IsDeleted);

            query = ApplyFilters(query, filters);

            return await query.CountAsync();
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
            transaction.CreatedAt = DateTime.Now;
            transaction.IsDeleted = false;

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            // Загружаем категорию
            await _context.Entry(transaction).Reference(t => t.Category).LoadAsync();

            return transaction;
        }

        /// <summary>
        /// Обновить операцию
        /// </summary>
        public async Task UpdateAsync(Transaction transaction)
        {
            transaction.UpdatedAt = DateTime.Now;

            _context.Entry(transaction).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Обновляем категорию
            await _context.Entry(transaction).Reference(t => t.Category).LoadAsync();
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
                // Включаем весь день DateTo
                var dateTo = filters.DateTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.Date <= dateTo);
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

            // Поиск по описанию
            if (!string.IsNullOrWhiteSpace(filters.SearchText))
            {
                var searchText = filters.SearchText.ToLower();
                query = query.Where(t => t.Description != null && t.Description.ToLower().Contains(searchText));
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

        #endregion
    }
}