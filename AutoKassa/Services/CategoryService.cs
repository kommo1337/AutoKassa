using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с категориями
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private static readonly ILogger _log = Log.ForContext<CategoryService>();

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
                .AsNoTracking()
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
                .AsNoTracking()
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
            var query = _context.Categories.AsNoTracking().Where(c => c.Type == type);

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
            if (string.IsNullOrWhiteSpace(category.Name))
                throw new ArgumentException("Название категории не может быть пустым", nameof(category));

            if (await ExistsAsync(category.Name, category.Type))
                throw new InvalidOperationException($"Категория с названием «{category.Name}» уже существует");

            category.CreatedAt = DateTime.Now;
            category.IsActive = true;

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _log.Information("Добавлена категория ID={Id}, название={Name}, тип={Type}", category.Id, category.Name, category.Type);
            return category;
        }

        /// <summary>
        /// Обновить категорию
        /// </summary>
        public async Task UpdateAsync(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                throw new ArgumentException("Название категории не может быть пустым", nameof(category));

            _context.Entry(category).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _log.Information("Обновлена категория ID={Id}, название={Name}", category.Id, category.Name);
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
                _log.Warning("Попытка удалить категорию ID={Id} с привязанными операциями — отклонено", id);
                return false;
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            _log.Information("Удалена категория ID={Id}", id);
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