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
                .ToListAsync()
                .ConfigureAwait(false);
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
                .ToListAsync()
                .ConfigureAwait(false);
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
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Получить категорию по ID
        /// </summary>
        public async Task<Category> GetByIdAsync(int id)
        {
            return await _context.Categories.FindAsync(id).ConfigureAwait(false);
        }

        /// <summary>
        /// Добавить новую категорию
        /// </summary>
        public async Task<Category> AddAsync(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                throw new ArgumentException("Название категории не может быть пустым", nameof(category));

            if (await ExistsAsync(category.Name, category.Type).ConfigureAwait(false))
                throw new InvalidOperationException($"Категория с названием «{category.Name}» уже существует");

            category.CreatedAt = DateTime.Now;
            category.IsActive = true;

            _context.Categories.Add(category);
            await _context.SaveChangesAsync().ConfigureAwait(false);

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

            if (await ExistsAsync(category.Name, category.Type, category.Id).ConfigureAwait(false))
                throw new InvalidOperationException($"Категория с названием «{category.Name}» уже существует");

            _context.Update(category);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _log.Information("Обновлена категория ID={Id}, название={Name}", category.Id, category.Name);
        }

        /// <summary>
        /// Удалить категорию
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id).ConfigureAwait(false);
            if (category == null) return false;

            // Нельзя удалить категорию, установленную по умолчанию в настройках
            var settings = await _context.AppSettings.FirstOrDefaultAsync().ConfigureAwait(false);
            if (settings?.DefaultIncomeCategoryId == id || settings?.DefaultExpenseCategoryId == id)
            {
                _log.Warning("Попытка удалить дефолтную категорию ID={Id} — отклонено", id);
                throw new InvalidOperationException("Нельзя удалить категорию, установленную по умолчанию. Сначала измените категорию по умолчанию в настройках.");
            }

            // Проверяем, есть ли связанные операции (включая удаленные)
            var hasTransactions = await _context.Transactions
                .AnyAsync(t => t.CategoryId == id)
                .ConfigureAwait(false);

            if (hasTransactions)
            {
                _log.Warning("Попытка удалить категорию ID={Id} с привязанными операциями — отклонено", id);
                return false;
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _log.Information("Удалена категория ID={Id}", id);
            return true;
        }

        /// <summary>
        /// Деактивировать категорию
        /// </summary>
        public async Task DeactivateAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id).ConfigureAwait(false);
            if (category != null)
            {
                category.IsActive = false;
                await _context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Получить количество операций по категории
        /// </summary>
        public async Task<int> GetOperationCountAsync(int categoryId)
        {
            return await _context.Transactions
                .Where(t => t.CategoryId == categoryId && !t.IsDeleted)
                .CountAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Проверить существование категории с таким именем и типом
        /// </summary>
        public async Task<bool> ExistsAsync(string name, OperationType type, int? excludeId = null)
        {
            // Быстрая проверка exact match на стороне БД (O(1) SQL-запрос).
            var exactMatch = await _context.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Type == type && c.Name == name &&
                               (!excludeId.HasValue || c.Id != excludeId.Value))
                .ConfigureAwait(false);

            if (exactMatch) return true;

            // Fallback: case-insensitive проверка в памяти для unicode/кириллицы,
            // т.к. SQLite LOWER() / COLLATE не гарантируют корректное поведение с кириллицей.
            var categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Type == type)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync()
                .ConfigureAwait(false);

            return categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                                       (!excludeId.HasValue || c.Id != excludeId.Value));
        }

        public async Task ReorderAsync(List<(int Id, int SortOrder)> updates)
        {
            var ids = updates.Select(u => u.Id).ToList();
            var categories = await _context.Categories
                .Where(c => ids.Contains(c.Id))
                .ToListAsync()
                .ConfigureAwait(false);

            var categoryDict = categories.ToDictionary(c => c.Id);
            foreach (var (id, sortOrder) in updates)
            {
                if (categoryDict.TryGetValue(id, out var category))
                {
                    category.SortOrder = sortOrder;
                }
            }
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}