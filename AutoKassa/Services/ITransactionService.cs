using AutoKassa.Models;
using AutoKassa.Models.Enums;

namespace AutoKassa.Services
{
    /// <summary>
    /// Интерфейс сервиса для работы с финансовыми операциями
    /// </summary>
    public interface ITransactionService
    {
        /// <summary>
        /// Получить список операций с фильтрацией
        /// </summary>
        Task<List<Transaction>> GetTransactionsAsync(TransactionFilterParameters filters, CancellationToken ct = default);

        /// <summary>
        /// Получить общее количество операций с учетом фильтров
        /// </summary>
        Task<int> GetTotalCountAsync(TransactionFilterParameters filters, CancellationToken ct = default);

        /// <summary>
        /// Получить операцию по ID
        /// </summary>
        Task<Transaction> GetByIdAsync(int id);

        /// <summary>
        /// Добавить новую операцию
        /// </summary>
        Task<Transaction> AddAsync(Transaction transaction);

        /// <summary>
        /// Обновить операцию
        /// </summary>
        Task UpdateAsync(Transaction transaction);

        /// <summary>
        /// Удалить операцию (soft delete)
        /// </summary>
        Task DeleteAsync(int id);

        /// <summary>
        /// Восстановить ранее удалённую операцию (отмена soft delete)
        /// </summary>
        Task RestoreAsync(int id);

        /// <summary>
        /// Получить последние N операций
        /// </summary>
        Task<List<Transaction>> GetRecentAsync(int count = 10);

        /// <summary>
        /// Получить суммарные доходы/расходы за период (SQL-агрегация, без загрузки всех записей)
        /// </summary>
        Task<(decimal Income, decimal Expense, int IncomeCount, int ExpenseCount)> GetPeriodTotalsAsync(DateTime from, DateTime to, PaymentType? paymentType = null, CancellationToken ct = default);

        /// <summary>
        /// Получить суммы доходов/расходов по дням за период (для графиков и группировки)
        /// </summary>
        Task<List<DailyTotalsItem>> GetDailyTotalsAsync(DateTime from, DateTime to, PaymentType? paymentType = null, CancellationToken ct = default);

        /// <summary>
        /// Получить топ N категорий по сумме для указанного типа операции
        /// </summary>
        Task<List<(string Name, decimal Total)>> GetTopCategoriesAsync(DateTime from, DateTime to, OperationType type, int count, PaymentType? paymentType = null, CancellationToken ct = default);
    }
}