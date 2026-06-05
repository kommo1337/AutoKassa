using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class TransactionServiceTests : IDisposable
    {
        private readonly AppDbContext _ctx;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly TransactionService _svc;

        public TransactionServiceTests()
        {
            var (factory, conn) = TestDatabase.CreateWithFactory();
            _conn = conn;
            _ctx = factory.CreateDbContext();
            _svc = new TransactionService(factory);
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conn.Dispose();
        }

        // ─────────────────────────────────────────
        // CRUD + Soft delete
        // ─────────────────────────────────────────

        [Fact]
        public async Task AddAsync_SetsCreatedAtAndIsDeletedFalse()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var t = await _svc.AddAsync(new Transaction
            {
                CategoryId  = cat.Id,
                Amount      = 500m,
                Type        = OperationType.Expense,
                Date        = DateTime.Today,
                Description = string.Empty
            });

            t.CreatedAt.Should().NotBe(default);
            t.IsDeleted.Should().BeFalse();
            t.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AddAsync_LoadsCategory()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var t = await _svc.AddAsync(new Transaction
            {
                CategoryId  = cat.Id,
                Amount      = 100m,
                Type        = OperationType.Expense,
                Date        = DateTime.Today,
                Description = string.Empty
            });

            t.Category.Should().NotBeNull();
            t.Category!.Id.Should().Be(cat.Id);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsCorrectTransaction()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var added = TestDatabase.SeedTransaction(_ctx, cat.Id, 200m);

            var result = await _svc.GetByIdAsync(added.Id);

            result.Should().NotBeNull();
            result!.Id.Should().Be(added.Id);
            result.Amount.Should().Be(200m);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenSoftDeleted()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var t = TestDatabase.SeedTransaction(_ctx, cat.Id, 300m);

            await _svc.DeleteAsync(t.Id);
            var result = await _svc.GetByIdAsync(t.Id);

            result.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_SetsIsDeletedTrue_RecordStaysInDb()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var t = TestDatabase.SeedTransaction(_ctx, cat.Id, 400m);

            await _svc.DeleteAsync(t.Id);

            var raw = await _ctx.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == t.Id);
            raw.Should().NotBeNull();
            raw!.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task RestoreAsync_SetsIsDeletedFalse()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var t = TestDatabase.SeedTransaction(_ctx, cat.Id, 500m);
            await _svc.DeleteAsync(t.Id);

            await _svc.RestoreAsync(t.Id);

            var restored = await _svc.GetByIdAsync(t.Id);
            restored.Should().NotBeNull();
            restored!.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateAsync_PersistsChanges()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var t = TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, description: "до");

            t.Amount      = 999m;
            t.Description = "после";
            await _svc.UpdateAsync(t);

            var updated = await _svc.GetByIdAsync(t.Id);
            updated!.Amount.Should().Be(999m);
            updated.Description.Should().Be("после");
        }

        [Fact]
        public async Task UpdateAsync_DetachedEntity_DoesNotThrowTrackingConflict()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var original = TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, description: "до");

            // Симулируем detached entity из UI (как после AsNoTracking)
            var detached = new Transaction
            {
                Id = original.Id,
                Date = original.Date,
                Amount = 999m,
                Type = original.Type,
                CategoryId = original.CategoryId,
                Description = "после",
                PaymentType = original.PaymentType,
                CreatedAt = original.CreatedAt,
                IsDeleted = original.IsDeleted
            };

            await _svc.UpdateAsync(detached);

            var updated = await _svc.GetByIdAsync(original.Id);
            updated!.Amount.Should().Be(999m);
            updated.Description.Should().Be("после");
        }

        // ─────────────────────────────────────────
        // Фильтры GetTransactionsAsync
        // ─────────────────────────────────────────

        [Fact]
        public async Task Filter_ExcludesSoftDeleted()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var t1 = TestDatabase.SeedTransaction(_ctx, cat.Id, 100m);
            var t2 = TestDatabase.SeedTransaction(_ctx, cat.Id, 200m);
            await _svc.DeleteAsync(t2.Id);

            var result = await _svc.GetTransactionsAsync(new TransactionFilterParameters { Take = 100 });

            result.Should().ContainSingle(t => t.Id == t1.Id);
            result.Should().NotContain(t => t.Id == t2.Id);
        }

        [Fact]
        public async Task Filter_ByDateRange_ReturnsOnlyInRange()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var inside  = TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, date: new DateTime(2025, 6, 15));
            var outside = TestDatabase.SeedTransaction(_ctx, cat.Id, 200m, date: new DateTime(2025, 7, 1));

            var result = await _svc.GetTransactionsAsync(new TransactionFilterParameters
            {
                DateFrom = new DateTime(2025, 6, 1),
                DateTo   = new DateTime(2025, 6, 30),
                Take     = 100
            });

            result.Should().ContainSingle(t => t.Id == inside.Id);
            result.Should().NotContain(t => t.Id == outside.Id);
        }

        [Fact]
        public async Task Filter_ByType_ReturnsOnlyMatchingType()
        {
            var expCat = TestDatabase.SeedExpenseCategory(_ctx);
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            var expense = TestDatabase.SeedTransaction(_ctx, expCat.Id, 100m, OperationType.Expense);
            var income  = TestDatabase.SeedTransaction(_ctx, incCat.Id, 200m, OperationType.Income);

            var expenses = await _svc.GetTransactionsAsync(new TransactionFilterParameters { Type = OperationType.Expense, Take = 100 });
            var incomes  = await _svc.GetTransactionsAsync(new TransactionFilterParameters { Type = OperationType.Income,  Take = 100 });

            expenses.Should().Contain(t => t.Id == expense.Id);
            expenses.Should().NotContain(t => t.Id == income.Id);
            incomes.Should().Contain(t => t.Id == income.Id);
            incomes.Should().NotContain(t => t.Id == expense.Id);
        }

        [Fact]
        public async Task Filter_ByPaymentType_Cash()
        {
            var cat  = TestDatabase.SeedExpenseCategory(_ctx);
            var cash = TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, paymentType: PaymentType.Cash);
            var card = TestDatabase.SeedTransaction(_ctx, cat.Id, 200m, paymentType: PaymentType.NonCash);

            var result = await _svc.GetTransactionsAsync(new TransactionFilterParameters { PaymentType = PaymentType.Cash, Take = 100 });

            result.Should().Contain(t => t.Id == cash.Id);
            result.Should().NotContain(t => t.Id == card.Id);
        }

        [Fact]
        public async Task Filter_ByCategory()
        {
            var cat1 = TestDatabase.SeedExpenseCategory(_ctx, "Кат-А");
            var cat2 = TestDatabase.SeedExpenseCategory(_ctx, "Кат-Б");
            var t1 = TestDatabase.SeedTransaction(_ctx, cat1.Id, 100m);
            var t2 = TestDatabase.SeedTransaction(_ctx, cat2.Id, 200m);

            var result = await _svc.GetTransactionsAsync(new TransactionFilterParameters { CategoryId = cat1.Id, Take = 100 });

            result.Should().ContainSingle(t => t.Id == t1.Id);
            result.Should().NotContain(t => t.Id == t2.Id);
        }

        [Fact]
        public async Task Filter_BySearchText_CaseInsensitive()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var match    = TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, description: "Замена масла");
            var noMatch  = TestDatabase.SeedTransaction(_ctx, cat.Id, 200m, description: "Диагностика");

            var result = await _svc.GetTransactionsAsync(new TransactionFilterParameters { SearchText = "МАСЛА", Take = 100 });

            result.Should().Contain(t => t.Id == match.Id);
            result.Should().NotContain(t => t.Id == noMatch.Id);
        }

        [Fact]
        public async Task Filter_ByAmountRange()
        {
            var cat  = TestDatabase.SeedExpenseCategory(_ctx);
            var low  = TestDatabase.SeedTransaction(_ctx, cat.Id, 100m);
            var mid  = TestDatabase.SeedTransaction(_ctx, cat.Id, 500m);
            var high = TestDatabase.SeedTransaction(_ctx, cat.Id, 1000m);

            var result = await _svc.GetTransactionsAsync(new TransactionFilterParameters
            {
                AmountFrom = 200m,
                AmountTo   = 700m,
                Take       = 100
            });

            result.Should().Contain(t => t.Id == mid.Id);
            result.Should().NotContain(t => t.Id == low.Id);
            result.Should().NotContain(t => t.Id == high.Id);
        }

        [Fact]
        public async Task Filter_Pagination_SkipTake()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            for (int i = 0; i < 5; i++)
                TestDatabase.SeedTransaction(_ctx, cat.Id, (i + 1) * 100m,
                    date: new DateTime(2025, 1, i + 1));

            var page1 = await _svc.GetTransactionsAsync(new TransactionFilterParameters { Skip = 0, Take = 2 });
            var page2 = await _svc.GetTransactionsAsync(new TransactionFilterParameters { Skip = 2, Take = 2 });

            page1.Should().HaveCount(2);
            page2.Should().HaveCount(2);
            page1.Select(t => t.Id).Should().NotIntersectWith(page2.Select(t => t.Id));
        }

        [Fact]
        public async Task GetTotalCountAsync_MatchesFilteredCount()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, date: new DateTime(2025, 3, 1));
            TestDatabase.SeedTransaction(_ctx, cat.Id, 200m, date: new DateTime(2025, 3, 15));
            TestDatabase.SeedTransaction(_ctx, cat.Id, 300m, date: new DateTime(2025, 4, 1));

            var filters = new TransactionFilterParameters
            {
                DateFrom = new DateTime(2025, 3, 1),
                DateTo   = new DateTime(2025, 3, 31),
                Take     = 100
            };

            var list  = await _svc.GetTransactionsAsync(filters);
            var count = await _svc.GetTotalCountAsync(filters);

            count.Should().Be(list.Count);
            count.Should().Be(2);
        }

        // ─────────────────────────────────────────
        // Агрегации
        // ─────────────────────────────────────────

        [Fact]
        public async Task GetPeriodTotalsAsync_CorrectSums()
        {
            var expCat = TestDatabase.SeedExpenseCategory(_ctx);
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            var day = new DateTime(2025, 5, 10);

            TestDatabase.SeedTransaction(_ctx, incCat.Id, 1000m, OperationType.Income,  date: day);
            TestDatabase.SeedTransaction(_ctx, incCat.Id, 500m,  OperationType.Income,  date: day);
            TestDatabase.SeedTransaction(_ctx, expCat.Id, 300m,  OperationType.Expense, date: day);

            var (income, expense, incCount, expCount) =
                await _svc.GetPeriodTotalsAsync(day, day);

            income.Should().Be(1500m);
            expense.Should().Be(300m);
            incCount.Should().Be(2);
            expCount.Should().Be(1);
        }

        [Fact]
        public async Task GetPeriodTotalsAsync_ExcludesSoftDeleted()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var day = new DateTime(2025, 5, 20);

            var kept    = TestDatabase.SeedTransaction(_ctx, cat.Id, 400m, date: day);
            var deleted = TestDatabase.SeedTransaction(_ctx, cat.Id, 600m, date: day);
            await _svc.DeleteAsync(deleted.Id);

            var (_, expense, _, expCount) = await _svc.GetPeriodTotalsAsync(day, day);

            expense.Should().Be(400m);
            expCount.Should().Be(1);
        }

        [Fact]
        public async Task GetPeriodTotalsAsync_EmptyPeriod_ReturnsZeros()
        {
            var (income, expense, incCount, expCount) =
                await _svc.GetPeriodTotalsAsync(new DateTime(2000, 1, 1), new DateTime(2000, 1, 31));

            income.Should().Be(0m);
            expense.Should().Be(0m);
            incCount.Should().Be(0);
            expCount.Should().Be(0);
        }

        [Fact]
        public async Task GetDailyTotalsAsync_GroupsByDate()
        {
            var cat  = TestDatabase.SeedExpenseCategory(_ctx);
            var day1 = new DateTime(2025, 6, 1);
            var day2 = new DateTime(2025, 6, 2);

            TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, date: day1);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 200m, date: day1);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 300m, date: day2);

            var result = await _svc.GetDailyTotalsAsync(day1, day2);

            result.Should().HaveCount(2);
            result.First(d => d.Date == day1).Expense.Should().Be(300m);
            result.First(d => d.Date == day2).Expense.Should().Be(300m);
        }

        [Fact]
        public async Task GetTopCategoriesAsync_ReturnsSortedByTotalDescending()
        {
            var cat1 = TestDatabase.SeedExpenseCategory(_ctx, "Большой");
            var cat2 = TestDatabase.SeedExpenseCategory(_ctx, "Маленький");
            var day = new DateTime(2025, 7, 1);

            TestDatabase.SeedTransaction(_ctx, cat1.Id, 1000m, date: day);
            TestDatabase.SeedTransaction(_ctx, cat2.Id, 100m,  date: day);

            var top = await _svc.GetTopCategoriesAsync(day, day, OperationType.Expense, 5);

            top.Should().HaveCount(2);
            top[0].Name.Should().Be("Большой");
            top[1].Name.Should().Be("Маленький");
        }

        [Fact]
        public async Task GetTopCategoriesAsync_RespectsCountLimit()
        {
            var day = new DateTime(2025, 8, 1);
            for (int i = 0; i < 5; i++)
            {
                var cat = TestDatabase.SeedExpenseCategory(_ctx, $"Кат-{i}");
                TestDatabase.SeedTransaction(_ctx, cat.Id, (i + 1) * 100m, date: day);
            }

            var top3 = await _svc.GetTopCategoriesAsync(day, day, OperationType.Expense, 3);

            top3.Should().HaveCount(3);
        }

        // ─────────────────────────────────────────
        // CancellationToken
        // ─────────────────────────────────────────

        [Fact]
        public async Task GetTransactionsAsync_AlreadyCancelled_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = () => _svc.GetTransactionsAsync(
                new TransactionFilterParameters { Take = 100 }, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task GetPeriodTotalsAsync_AlreadyCancelled_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = () => _svc.GetPeriodTotalsAsync(
                DateTime.Today, DateTime.Today, null, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        // ─────────────────────────────────────────
        // Дополнительные тесты
        // ─────────────────────────────────────────

        [Fact]
        public async Task GetRecentAsync_ReturnsRequestedCount_OrderedByDateDesc()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            for (int i = 0; i < 5; i++)
                TestDatabase.SeedTransaction(_ctx, cat.Id, (i + 1) * 100m,
                    date: new DateTime(2025, 1, i + 1));

            var result = await _svc.GetRecentAsync(3);

            result.Should().HaveCount(3);
            result[0].Date.Should().BeOnOrAfter(result[1].Date);
            result[1].Date.Should().BeOnOrAfter(result[2].Date);
        }

        [Fact]
        public async Task GetRecentAsync_ExcludesSoftDeleted()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var kept = TestDatabase.SeedTransaction(_ctx, cat.Id, 100m);
            var deleted = TestDatabase.SeedTransaction(_ctx, cat.Id, 200m);
            await _svc.DeleteAsync(deleted.Id);

            var result = await _svc.GetRecentAsync(10);

            result.Should().ContainSingle(t => t.Id == kept.Id);
            result.Should().NotContain(t => t.Id == deleted.Id);
        }

        [Fact]
        public async Task GetRecentAsync_LoadsCategory()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Загружаемая");
            TestDatabase.SeedTransaction(_ctx, cat.Id, 100m);

            var result = await _svc.GetRecentAsync(1);

            result[0].Category.Should().NotBeNull();
            result[0].Category!.Name.Should().Be("Загружаемая");
        }

        [Fact]
        public async Task Filter_CombinedFilters_TypeAndDateAndPayment()
        {
            var expCat = TestDatabase.SeedExpenseCategory(_ctx);
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            var day = new DateTime(2025, 6, 15);

            var match = TestDatabase.SeedTransaction(_ctx, expCat.Id, 100m, OperationType.Expense, PaymentType.Cash, day);
            TestDatabase.SeedTransaction(_ctx, expCat.Id, 200m, OperationType.Expense, PaymentType.NonCash, day);
            TestDatabase.SeedTransaction(_ctx, incCat.Id, 300m, OperationType.Income, PaymentType.Cash, day);
            TestDatabase.SeedTransaction(_ctx, expCat.Id, 400m, OperationType.Expense, PaymentType.Cash, new DateTime(2025, 7, 1));

            var result = await _svc.GetTransactionsAsync(new TransactionFilterParameters
            {
                Type = OperationType.Expense,
                PaymentType = PaymentType.Cash,
                DateFrom = new DateTime(2025, 6, 1),
                DateTo = new DateTime(2025, 6, 30),
                Take = 100
            });

            result.Should().ContainSingle(t => t.Id == match.Id);
        }

        [Fact]
        public async Task Filter_CombinedFilters_CategoryAndSearchText()
        {
            var cat1 = TestDatabase.SeedExpenseCategory(_ctx, "Фильтр-Кат1");
            var cat2 = TestDatabase.SeedExpenseCategory(_ctx, "Фильтр-Кат2");

            var match = TestDatabase.SeedTransaction(_ctx, cat1.Id, 100m, description: "замена масла");
            TestDatabase.SeedTransaction(_ctx, cat1.Id, 200m, description: "диагностика");
            TestDatabase.SeedTransaction(_ctx, cat2.Id, 300m, description: "замена масла");

            var result = await _svc.GetTransactionsAsync(new TransactionFilterParameters
            {
                CategoryId = cat1.Id,
                SearchText = "масла",
                Take = 100
            });

            result.Should().ContainSingle(t => t.Id == match.Id);
        }

        [Fact]
        public async Task GetTransactionsAsync_DefaultSorting_ByDateDescending()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, date: new DateTime(2025, 1, 1));
            TestDatabase.SeedTransaction(_ctx, cat.Id, 200m, date: new DateTime(2025, 3, 1));
            TestDatabase.SeedTransaction(_ctx, cat.Id, 300m, date: new DateTime(2025, 2, 1));

            var result = await _svc.GetTransactionsAsync(new TransactionFilterParameters { Take = 100 });

            result[0].Date.Should().BeOnOrAfter(result[1].Date);
            result[1].Date.Should().BeOnOrAfter(result[2].Date);
        }
    }
}
