using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class CategoryServiceTests : IDisposable
    {
        private readonly AppDbContext _ctx;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly CategoryService _svc;

        public CategoryServiceTests()
        {
            var (factory, conn) = TestDatabase.CreateWithFactory();
            _conn = conn;
            _ctx = factory.CreateDbContext();
            _svc = new CategoryService(factory);
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conn.Dispose();
        }

        // ─────────────────────────────────────────
        // CRUD
        // ─────────────────────────────────────────

        [Fact]
        public async Task AddAsync_CreatesCategory_WithCorrectFields()
        {
            var cat = await _svc.AddAsync(new Category
            {
                Name  = "Новая категория",
                Type  = OperationType.Expense,
                Color = "#123456"
            });

            cat.Id.Should().BeGreaterThan(0);
            cat.IsActive.Should().BeTrue();
            cat.CreatedAt.Should().NotBe(default);
            cat.Name.Should().Be("Новая категория");
        }

        [Fact]
        public async Task GetByTypeAsync_ReturnsOnlyMatchingType()
        {
            TestDatabase.SeedExpenseCategory(_ctx, "Расход-А");
            TestDatabase.SeedIncomeCategory(_ctx, "Доход-А");

            var expenses = await _svc.GetByTypeAsync(OperationType.Expense, activeOnly: false);
            var incomes  = await _svc.GetByTypeAsync(OperationType.Income,  activeOnly: false);

            expenses.Should().OnlyContain(c => c.Type == OperationType.Expense);
            incomes.Should().OnlyContain(c => c.Type == OperationType.Income);
        }

        [Fact]
        public async Task GetByTypeAsync_ActiveOnly_ExcludesInactiveCategories()
        {
            var active   = TestDatabase.SeedExpenseCategory(_ctx, "Активная");
            var inactive = TestDatabase.SeedExpenseCategory(_ctx, "Неактивная");
            await _svc.DeactivateAsync(inactive.Id);

            var result = await _svc.GetByTypeAsync(OperationType.Expense, activeOnly: true);

            result.Should().Contain(c => c.Id == active.Id);
            result.Should().NotContain(c => c.Id == inactive.Id);
        }

        [Fact]
        public async Task GetByTypeAsync_ActiveFalse_IncludesInactiveCategories()
        {
            var inactive = TestDatabase.SeedExpenseCategory(_ctx, "Неактивная-2");
            await _svc.DeactivateAsync(inactive.Id);

            var result = await _svc.GetByTypeAsync(OperationType.Expense, activeOnly: false);

            result.Should().Contain(c => c.Id == inactive.Id);
        }

        // ─────────────────────────────────────────
        // ExistsAsync
        // ─────────────────────────────────────────

        [Fact]
        public async Task ExistsAsync_ReturnsTrueForExactDuplicate()
        {
            TestDatabase.SeedExpenseCategory(_ctx, "Запчасти-тест");

            var exists = await _svc.ExistsAsync("Запчасти-тест", OperationType.Expense);

            exists.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_ReturnsFalseForDifferentType()
        {
            TestDatabase.SeedExpenseCategory(_ctx, "Одно-имя");

            // То же имя, но другой тип — не считается дублём
            var exists = await _svc.ExistsAsync("Одно-имя", OperationType.Income);

            exists.Should().BeFalse();
        }

        [Fact]
        public async Task ExistsAsync_ExcludesIdWhenUpdating()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Редактируемая");

            // Проверяем, что категория не считает себя дублём при редактировании
            var exists = await _svc.ExistsAsync("Редактируемая", OperationType.Expense, excludeId: cat.Id);

            exists.Should().BeFalse();
        }

        // ─────────────────────────────────────────
        // Delete / Deactivate
        // ─────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_SucceedsIfNoTransactions()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Пустая");

            var result = await _svc.DeleteAsync(cat.Id);

            result.Should().BeTrue();
            var found = await _svc.GetByIdAsync(cat.Id);
            found.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_FailsIfTransactionsExist()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Занятая");
            TestDatabase.SeedTransaction(_ctx, cat.Id, 100m);

            var result = await _svc.DeleteAsync(cat.Id);

            result.Should().BeFalse();
            var found = await _svc.GetByIdAsync(cat.Id);
            found.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteAsync_FailsIfOnlySoftDeletedTransactionsExist()
        {
            // Даже soft-deleted транзакции блокируют удаление категории
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Soft-занятая");
            var t = TestDatabase.SeedTransaction(_ctx, cat.Id, 100m);
            _ctx.Transactions.Find(t.Id)!.IsDeleted = true;
            _ctx.SaveChanges();

            var result = await _svc.DeleteAsync(cat.Id);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeactivateAsync_SetsIsActiveFalse()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Деактивируемая");
            cat.IsActive.Should().BeTrue();

            await _svc.DeactivateAsync(cat.Id);

            var updated = await _svc.GetByIdAsync(cat.Id);
            updated!.IsActive.Should().BeFalse();
        }

        // ─────────────────────────────────────────
        // ReorderAsync
        // ─────────────────────────────────────────

        [Fact]
        public async Task ReorderAsync_UpdatesSortOrders()
        {
            var cat1 = TestDatabase.SeedExpenseCategory(_ctx, "Ред-1");
            var cat2 = TestDatabase.SeedExpenseCategory(_ctx, "Ред-2");

            await _svc.ReorderAsync(new List<(int, int)>
            {
                (cat1.Id, 99),
                (cat2.Id, 1)
            });

            var updated1 = await _svc.GetByIdAsync(cat1.Id);
            var updated2 = await _svc.GetByIdAsync(cat2.Id);

            updated1!.SortOrder.Should().Be(99);
            updated2!.SortOrder.Should().Be(1);
        }

        // ─────────────────────────────────────────
        // Дополнительные тесты
        // ─────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_ReturnsBothTypes()
        {
            TestDatabase.SeedExpenseCategory(_ctx, "Расход-X");
            TestDatabase.SeedIncomeCategory(_ctx, "Доход-X");

            var all = await _svc.GetAllAsync();

            all.Should().Contain(c => c.Type == OperationType.Expense);
            all.Should().Contain(c => c.Type == OperationType.Income);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsCorrectCategory()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Точная");

            var result = await _svc.GetByIdAsync(cat.Id);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Точная");
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
        {
            var result = await _svc.GetByIdAsync(99999);

            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_PersistsNameAndColorChanges()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "До-изменения");
            cat.Name = "После-изменения";
            cat.Color = "#ff0000";

            await _svc.UpdateAsync(cat);

            var updated = await _svc.GetByIdAsync(cat.Id);
            updated!.Name.Should().Be("После-изменения");
            updated.Color.Should().Be("#ff0000");
        }

        [Fact]
        public async Task UpdateAsync_DoesNotThrowForValidData()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Валидная");
            cat.Name = "Новое-имя";

            Func<Task> act = () => _svc.UpdateAsync(cat);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UpdateAsync_DetachedEntity_DoesNotThrowTrackingConflict()
        {
            var original = TestDatabase.SeedExpenseCategory(_ctx, "До-Detached");

            // Симулируем detached entity из UI (как после AsNoTracking)
            var detached = new Category
            {
                Id = original.Id,
                Name = "После-Detached",
                Type = original.Type,
                Color = "#ff0000",
                SortOrder = original.SortOrder,
                IsActive = original.IsActive,
                IsSystem = original.IsSystem,
                CreatedAt = original.CreatedAt
            };

            await _svc.UpdateAsync(detached);

            var updated = await _svc.GetByIdAsync(original.Id);
            updated!.Name.Should().Be("После-Detached");
            updated.Color.Should().Be("#ff0000");
        }

        [Fact]
        public async Task GetOperationCountAsync_CountsOnlyNonDeletedTransactions()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Счётная");
            TestDatabase.SeedTransaction(_ctx, cat.Id, 100m);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 200m);
            var deleted = TestDatabase.SeedTransaction(_ctx, cat.Id, 300m);
            _ctx.Transactions.Find(deleted.Id)!.IsDeleted = true;
            _ctx.SaveChanges();

            var count = await _svc.GetOperationCountAsync(cat.Id);

            count.Should().Be(2);
        }
    }
}
