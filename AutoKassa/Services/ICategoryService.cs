using System.Collections.Generic;
using System.Threading.Tasks;
using AutoKassa.Models;
using AutoKassa.Models.Enums;

namespace AutoKassa.Services
{
    /// <summary>
    /// Интерфейс сервиса для работы с категориями
    /// </summary>
    public interface ICategoryService
    {
        /// <summary>
        /// Получить все категории
        /// </summary>
        Task<List<Category>> GetAllAsync();

        /// <summary>
        /// Получить активные категории
        /// </summary>
        Task<List<Category>> GetActiveAsync();

        /// <summary>
        /// Получить категории по типу
        /// </summary>
        Task<List<Category>> GetByTypeAsync(OperationType type, bool activeOnly = true);

        /// <summary>
        /// Получить категорию по ID
        /// </summary>
        Task<Category> GetByIdAsync(int id);

        /// <summary>
        /// Добавить новую категорию
        /// </summary>
        Task<Category> AddAsync(Category category);

        /// <summary>
        /// Обновить категорию
        /// </summary>
        Task UpdateAsync(Category category);

        /// <summary>
        /// Удалить категорию
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Деактивировать категорию
        /// </summary>
        Task DeactivateAsync(int id);

        /// <summary>
        /// Получить количество операций по категории
        /// </summary>
        Task<int> GetOperationCountAsync(int categoryId);

        /// <summary>
        /// Проверить существование категории с таким именем и типом
        /// </summary>
        Task<bool> ExistsAsync(string name, OperationType type, int? excludeId = null);
    }
}