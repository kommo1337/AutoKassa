using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoKassa.Models;

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
        Task<List<Transaction>> GetTransactionsAsync(TransactionFilterParameters filters);

        /// <summary>
        /// Получить общее количество операций с учетом фильтров
        /// </summary>
        Task<int> GetTotalCountAsync(TransactionFilterParameters filters);

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
        /// Получить последние N операций
        /// </summary>
        Task<List<Transaction>> GetRecentAsync(int count = 10);
    }
}