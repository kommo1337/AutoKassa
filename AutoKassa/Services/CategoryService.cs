using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с категориями
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _context;

        public CategoryService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Получить все категории
        /// </summary>
        public async Task<List<Category>> GetAllAsync()
        {
            return await _context.Categories
                .OrderBy(c => c.Type)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Получить активные категории
        /// </summary>
        public async Task<List<Category>> GetActiveAsync()
        {
            return await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Type)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Получить категории по типу
        /// </summary>
        public async Task<List<Category>> GetByTypeAsync(OperationType type, bool activeOnly = true)
        {
            var query = _context.Categories.Where(c => c.Type == type);

            if (activeOnly)
            {
                query = query.Where(c => c.IsActive);
            }

            return await query
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Получить категорию по ID
        /// </summary>
        public async Task<Category> GetByIdAsync(int id)
        {
            return await _context.Categories.FindAsync(id);
        }

        /// <summary>
        /// Добавить новую категорию
        /// </summary>
        public async Task<Category> AddAsync(Category category)
        {
            category.CreatedAt = DateTime.Now;
            category.IsActive = true;

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return category;
        }

        /// <summary>
        /// Обновить категорию
        /// </summary>
        public async Task UpdateAsync(Category category)
        {
            _context.Entry(category).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Удалить категорию
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return false;

            // Проверяем, есть ли связанные операции (включая удаленные)
            var hasTransactions = await _context.Transactions
                .AnyAsync(t => t.CategoryId == id);

            if (hasTransactions)
            {
                return false; // Нельзя удалить категорию с операциями
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Деактивировать категорию
        /// </summary>
        public async Task DeactivateAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                category.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Получить количество операций по категории
        /// </summary>
        public async Task<int> GetOperationCountAsync(int categoryId)
        {
            return await _context.Transactions
                .Where(t => t.CategoryId == categoryId && !t.IsDeleted)
                .CountAsync();
        }

        /// <summary>
        /// Проверить существование категории с таким именем и типом
        /// </summary>
        public async Task<bool> ExistsAsync(string name, OperationType type, int? excludeId = null)
        {
            var query = _context.Categories
                .Where(c => c.Name == name && c.Type == type);

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task ReorderAsync(List<(int Id, int SortOrder)> updates)
        {
            foreach (var (id, sortOrder) in updates)
            {
                var category = await _context.Categories.FindAsync(id);
                if (category != null)
                {
                    category.SortOrder = sortOrder;
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}