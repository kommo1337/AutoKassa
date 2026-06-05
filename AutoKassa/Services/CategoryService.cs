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

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CategoryService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Получить все категории
        /// </summary>
        public async Task<List<Category>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.Categories
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
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.Categories
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
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var query = context.Categories.AsNoTracking().Where(c => c.Type == type);

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
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.Categories.FindAsync(id).ConfigureAwait(false);
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

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            category.CreatedAt = DateTime.Now;
            category.IsActive = true;

            context.Categories.Add(category);
            await context.SaveChangesAsync().ConfigureAwait(false);

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

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            // Каждый вызов использует свой DbContext (unit of work), поэтому конфликтов
            // отслеживания быть не может.
            var existing = await context.Categories.FindAsync(category.Id).ConfigureAwait(false);
            if (existing == null)
                throw new InvalidOperationException($"Категория ID={category.Id} не найдена");

            existing.Name = category.Name;
            existing.Type = category.Type;
            existing.Color = category.Color;
            existing.SortOrder = category.SortOrder;
            existing.IsActive = category.IsActive;
            existing.IsSystem = category.IsSystem;

            await context.SaveChangesAsync().ConfigureAwait(false);

            _log.Information("Обновлена категория ID={Id}, название={Name}", category.Id, category.Name);
        }

        /// <summary>
        /// Удалить категорию
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var category = await context.Categories.FindAsync(id).ConfigureAwait(false);
            if (category == null) return false;

            // Нельзя удалить категорию, установленную по умолчанию в настройках
            var settings = await context.AppSettings.FirstOrDefaultAsync().ConfigureAwait(false);
            if (settings?.DefaultIncomeCategoryId == id || settings?.DefaultExpenseCategoryId == id)
            {
                _log.Warning("Попытка удалить дефолтную категорию ID={Id} — отклонено", id);
                throw new InvalidOperationException("Нельзя удалить категорию, установленную по умолчанию. Сначала измените категорию по умолчанию в настройках.");
            }

            // Проверяем, есть ли связанные операции (включая удаленные)
            var hasTransactions = await context.Transactions
                .AnyAsync(t => t.CategoryId == id)
                .ConfigureAwait(false);

            if (hasTransactions)
            {
                _log.Warning("Попытка удалить категорию ID={Id} с привязанными операциями — отклонено", id);
                return false;
            }

            context.Categories.Remove(category);
            await context.SaveChangesAsync().ConfigureAwait(false);

            _log.Information("Удалена категория ID={Id}", id);
            return true;
        }

        /// <summary>
        /// Деактивировать категорию
        /// </summary>
        public async Task DeactivateAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var category = await context.Categories.FindAsync(id).ConfigureAwait(false);
            if (category != null)
            {
                category.IsActive = false;
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Получить количество операций по категории
        /// </summary>
        public async Task<int> GetOperationCountAsync(int categoryId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.Transactions
                .Where(t => t.CategoryId == categoryId && !t.IsDeleted)
                .CountAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Проверить существование категории с таким именем и типом
        /// </summary>
        public async Task<bool> ExistsAsync(string name, OperationType type, int? excludeId = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            // Быстрая проверка exact match на стороне БД (O(1) SQL-запрос).
            var exactMatch = await context.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Type == type && c.Name == name &&
                               (!excludeId.HasValue || c.Id != excludeId.Value))
                .ConfigureAwait(false);

            if (exactMatch) return true;

            // Fallback: case-insensitive проверка в памяти для unicode/кириллицы,
            // т.к. SQLite LOWER() / COLLATE не гарантируют корректное поведение с кириллицей.
            var categories = await context.Categories
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
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var ids = updates.Select(u => u.Id).ToList();
            var categories = await context.Categories
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
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}