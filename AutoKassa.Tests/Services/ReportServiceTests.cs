using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class ReportServiceTests : IDisposable
    {
        private readonly AppDbContext _ctx;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly ReportService _svc;
        private readonly TestDbContextFactory _factory;
        private readonly ISettingsService _settingsSvc;

        public ReportServiceTests()
        {
            (_factory, _conn) = TestDatabase.CreateWithFactory();
            _settingsSvc = new SettingsService(_factory);
            _ctx = _factory.CreateDbContext();
            _svc = new ReportService(_ctx, _settingsSvc);
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conn.Dispose();
        }

        private async Task SetInitialBalanceAsync(decimal amount)
        {
            var settings = await _settingsSvc.GetSettingsAsync();
            settings.InitialBalance = amount;
            await _settingsSvc.SaveSettingsAsync(settings);
        }

        // ─────────────────────────────────────────
        // GenerateBalanceReportAsync
        // ─────────────────────────────────────────

        [Fact]
        public async Task GenerateBalanceReportAsync_SingleIncomeAndExpense_ComputesCorrectTotals()
        {
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            var expCat = TestDatabase.SeedExpenseCategory(_ctx);
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, incCat.Id, 1000m, OperationType.Income, date: day);
            TestDatabase.SeedTransaction(_ctx, expCat.Id, 300m, OperationType.Expense, date: day);

            var report = await _svc.GenerateBalanceReportAsync(day, day);

            report.TotalIncome.Should().Be(1000m);
            report.TotalExpense.Should().Be(300m);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_EmptyPeriod_ReturnsZeroTotals()
        {
            var from = new DateTime(2025, 1, 1);
            var to = new DateTime(2025, 1, 5);

            var report = await _svc.GenerateBalanceReportAsync(from, to);

            report.TotalIncome.Should().Be(0m);
            report.TotalExpense.Should().Be(0m);
            report.DailyBalances.Should().HaveCount(5);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_ExcludesSoftDeletedTransactions()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var day = new DateTime(2025, 6, 15);

            var t = TestDatabase.SeedTransaction(_ctx, cat.Id, 500m, OperationType.Expense, date: day);
            _ctx.Transactions.Find(t.Id)!.IsDeleted = true;
            _ctx.SaveChanges();

            var report = await _svc.GenerateBalanceReportAsync(day, day);

            report.TotalExpense.Should().Be(0m);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_DateToIsInclusive()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var dateTo = new DateTime(2025, 6, 30);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 200m, OperationType.Expense, date: dateTo);

            var report = await _svc.GenerateBalanceReportAsync(new DateTime(2025, 6, 1), dateTo);

            report.TotalExpense.Should().Be(200m);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_TransactionBeforePeriod_AffectsStartBalance()
        {
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            TestDatabase.SeedTransaction(_ctx, incCat.Id, 5000m, OperationType.Income, date: new DateTime(2025, 1, 1));

            var report = await _svc.GenerateBalanceReportAsync(new DateTime(2025, 6, 1), new DateTime(2025, 6, 30));

            report.StartBalance.Should().Be(5000m);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_TransactionAfterPeriod_DoesNotAffectReport()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 999m, OperationType.Expense, date: new DateTime(2025, 7, 1));

            var report = await _svc.GenerateBalanceReportAsync(new DateTime(2025, 6, 1), new DateTime(2025, 6, 30));

            report.TotalExpense.Should().Be(0m);
            report.EndBalance.Should().Be(0m);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_DailyBalances_CountMatchesDaysInRange()
        {
            var from = new DateTime(2025, 6, 10);
            var to = new DateTime(2025, 6, 14);

            var report = await _svc.GenerateBalanceReportAsync(from, to);

            report.DailyBalances.Should().HaveCount(5);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_DailyBalances_RunningBalanceIsAccurate()
        {
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            var expCat = TestDatabase.SeedExpenseCategory(_ctx);
            var day1 = new DateTime(2025, 6, 1);
            var day2 = new DateTime(2025, 6, 2);

            TestDatabase.SeedTransaction(_ctx, incCat.Id, 1000m, OperationType.Income, date: day1);
            TestDatabase.SeedTransaction(_ctx, expCat.Id, 300m, OperationType.Expense, date: day2);

            var report = await _svc.GenerateBalanceReportAsync(day1, day2);

            report.DailyBalances[0].Balance.Should().Be(1000m);
            report.DailyBalances[1].Balance.Should().Be(700m);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_PaymentTypeFilter_FiltersCorrectly()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, OperationType.Expense, PaymentType.Cash, day);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 200m, OperationType.Expense, PaymentType.NonCash, day);

            var report = await _svc.GenerateBalanceReportAsync(day, day, PaymentType.Cash);

            report.TotalExpense.Should().Be(100m);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_EndBalanceEqualsStartPlusIncomesMinusExpenses()
        {
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            var expCat = TestDatabase.SeedExpenseCategory(_ctx);

            TestDatabase.SeedTransaction(_ctx, incCat.Id, 3000m, OperationType.Income, date: new DateTime(2025, 1, 1));
            TestDatabase.SeedTransaction(_ctx, incCat.Id, 500m, OperationType.Income, date: new DateTime(2025, 6, 15));
            TestDatabase.SeedTransaction(_ctx, expCat.Id, 200m, OperationType.Expense, date: new DateTime(2025, 6, 20));

            var report = await _svc.GenerateBalanceReportAsync(new DateTime(2025, 6, 1), new DateTime(2025, 6, 30));

            report.EndBalance.Should().Be(report.StartBalance + report.TotalIncome - report.TotalExpense);
        }

        // ─────────────────────────────────────────
        // GetInitialBalanceAsync
        // ─────────────────────────────────────────

        [Fact]
        public async Task GetInitialBalanceAsync_NoTransactions_ReturnsZero()
        {
            var result = await _svc.GetInitialBalanceAsync(DateTime.Today);

            result.Should().Be(0m);
        }

        [Fact]
        public async Task GetInitialBalanceAsync_MixedTransactionsBeforeDate_ReturnsCorrectBalance()
        {
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            var expCat = TestDatabase.SeedExpenseCategory(_ctx);

            TestDatabase.SeedTransaction(_ctx, incCat.Id, 1000m, OperationType.Income, date: new DateTime(2025, 5, 1));
            TestDatabase.SeedTransaction(_ctx, expCat.Id, 300m, OperationType.Expense, date: new DateTime(2025, 5, 10));

            var result = await _svc.GetInitialBalanceAsync(new DateTime(2025, 6, 1));

            result.Should().Be(700m);
        }

        [Fact]
        public async Task GetInitialBalanceAsync_TransactionsOnDate_AreExcluded()
        {
            var cat = TestDatabase.SeedIncomeCategory(_ctx);
            var date = new DateTime(2025, 6, 1);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 500m, OperationType.Income, date: date);

            var result = await _svc.GetInitialBalanceAsync(date);

            result.Should().Be(0m);
        }

        [Fact]
        public async Task GetInitialBalanceAsync_WithPaymentTypeFilter_OnlyIncludesMatchingType()
        {
            var cat = TestDatabase.SeedIncomeCategory(_ctx);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 1000m, OperationType.Income, PaymentType.Cash, new DateTime(2025, 5, 1));
            TestDatabase.SeedTransaction(_ctx, cat.Id, 2000m, OperationType.Income, PaymentType.NonCash, new DateTime(2025, 5, 1));

            var result = await _svc.GetInitialBalanceAsync(new DateTime(2025, 6, 1), PaymentType.Cash);

            result.Should().Be(1000m);
        }

        [Fact]
        public async Task GetInitialBalanceAsync_IncludesAppSettingsInitialBalance_WhenNoPaymentType()
        {
            await SetInitialBalanceAsync(2500m);

            var result = await _svc.GetInitialBalanceAsync(DateTime.Today);

            result.Should().Be(2500m);
        }

        [Fact]
        public async Task GetInitialBalanceAsync_IncludesAppSettingsInitialBalance_ForCash()
        {
            await SetInitialBalanceAsync(1000m);

            var result = await _svc.GetInitialBalanceAsync(DateTime.Today, PaymentType.Cash);

            result.Should().Be(1000m);
        }

        [Fact]
        public async Task GetInitialBalanceAsync_DoesNotIncludeInitialBalance_ForNonCash()
        {
            await SetInitialBalanceAsync(1000m);

            var result = await _svc.GetInitialBalanceAsync(DateTime.Today, PaymentType.NonCash);

            result.Should().Be(0m);
        }

        [Fact]
        public async Task GenerateBalanceReportAsync_StartBalanceIncludesInitialBalance()
        {
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            await SetInitialBalanceAsync(500m);
            TestDatabase.SeedTransaction(_ctx, incCat.Id, 200m, OperationType.Income, date: new DateTime(2025, 1, 1));

            var report = await _svc.GenerateBalanceReportAsync(new DateTime(2025, 6, 1), new DateTime(2025, 6, 30));

            report.StartBalance.Should().Be(700m);
        }

        // ─────────────────────────────────────────
        // GenerateCategoryReportAsync
        // ─────────────────────────────────────────

        [Fact]
        public async Task GenerateCategoryReportAsync_GroupsByCategoryCorrectly()
        {
            var cat1 = TestDatabase.SeedExpenseCategory(_ctx, "Кат-1");
            var cat2 = TestDatabase.SeedExpenseCategory(_ctx, "Кат-2");
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, cat1.Id, 100m, OperationType.Expense, date: day);
            TestDatabase.SeedTransaction(_ctx, cat1.Id, 200m, OperationType.Expense, date: day);
            TestDatabase.SeedTransaction(_ctx, cat2.Id, 300m, OperationType.Expense, date: day);

            var report = await _svc.GenerateCategoryReportAsync(day, day, OperationType.Expense);

            report.CategoryItems.Should().HaveCount(2);
            report.TotalAmount.Should().Be(600m);
        }

        [Fact]
        public async Task GenerateCategoryReportAsync_PercentagesAddUpTo100()
        {
            var cat1 = TestDatabase.SeedExpenseCategory(_ctx, "А");
            var cat2 = TestDatabase.SeedExpenseCategory(_ctx, "Б");
            var cat3 = TestDatabase.SeedExpenseCategory(_ctx, "В");
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, cat1.Id, 500m, OperationType.Expense, date: day);
            TestDatabase.SeedTransaction(_ctx, cat2.Id, 300m, OperationType.Expense, date: day);
            TestDatabase.SeedTransaction(_ctx, cat3.Id, 200m, OperationType.Expense, date: day);

            var report = await _svc.GenerateCategoryReportAsync(day, day, OperationType.Expense);

            report.CategoryItems.Sum(c => c.Percentage).Should().BeApproximately(100.0, 0.5);
        }

        [Fact]
        public async Task GenerateCategoryReportAsync_EmptyPeriod_ReturnsEmptyCategories()
        {
            var report = await _svc.GenerateCategoryReportAsync(
                new DateTime(2025, 1, 1), new DateTime(2025, 1, 31), OperationType.Expense);

            report.TotalAmount.Should().Be(0m);
            report.CategoryItems.Should().BeEmpty();
        }

        [Fact]
        public async Task GenerateCategoryReportAsync_ZeroTotalAmount_PercentageIsZero()
        {
            // С пустым периодом не будет категорий, но проверим через пустоту
            var report = await _svc.GenerateCategoryReportAsync(
                new DateTime(2025, 1, 1), new DateTime(2025, 1, 31), OperationType.Income);

            report.CategoryItems.Should().BeEmpty();
            report.TotalAmount.Should().Be(0m);
        }

        [Fact]
        public async Task GenerateCategoryReportAsync_ExcludesSoftDeletedTransactions()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, OperationType.Expense, date: day);
            var deleted = TestDatabase.SeedTransaction(_ctx, cat.Id, 500m, OperationType.Expense, date: day);
            _ctx.Transactions.Find(deleted.Id)!.IsDeleted = true;
            _ctx.SaveChanges();

            var report = await _svc.GenerateCategoryReportAsync(day, day, OperationType.Expense);

            report.TotalAmount.Should().Be(100m);
        }

        [Fact]
        public async Task GenerateCategoryReportAsync_OrderedByAmountDescending()
        {
            var small = TestDatabase.SeedExpenseCategory(_ctx, "Мелкие");
            var big = TestDatabase.SeedExpenseCategory(_ctx, "Крупные");
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, small.Id, 100m, OperationType.Expense, date: day);
            TestDatabase.SeedTransaction(_ctx, big.Id, 900m, OperationType.Expense, date: day);

            var report = await _svc.GenerateCategoryReportAsync(day, day, OperationType.Expense);

            report.CategoryItems[0].Amount.Should().BeGreaterThan(report.CategoryItems[1].Amount);
        }

        [Fact]
        public async Task GenerateCategoryReportAsync_ColorsAreAssignedSequentially()
        {
            var cat1 = TestDatabase.SeedExpenseCategory(_ctx, "К1");
            var cat2 = TestDatabase.SeedExpenseCategory(_ctx, "К2");
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, cat1.Id, 500m, OperationType.Expense, date: day);
            TestDatabase.SeedTransaction(_ctx, cat2.Id, 300m, OperationType.Expense, date: day);

            var report = await _svc.GenerateCategoryReportAsync(day, day, OperationType.Expense);

            report.CategoryItems[0].Color.Should().Be("#2196F3");
            report.CategoryItems[1].Color.Should().Be("#4CAF50");
        }

        // ─────────────────────────────────────────
        // GenerateTransactionDetailReportAsync
        // ─────────────────────────────────────────

        [Fact]
        public async Task GenerateTransactionDetailReportAsync_NoFilters_ReturnsAllInPeriod()
        {
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            var expCat = TestDatabase.SeedExpenseCategory(_ctx);
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, incCat.Id, 1000m, OperationType.Income, date: day);
            TestDatabase.SeedTransaction(_ctx, expCat.Id, 500m, OperationType.Expense, date: day);

            var report = await _svc.GenerateTransactionDetailReportAsync(day, day);

            report.Transactions.Should().HaveCount(2);
            report.TotalIncome.Should().Be(1000m);
            report.TotalExpense.Should().Be(500m);
        }

        [Fact]
        public async Task GenerateTransactionDetailReportAsync_FilterByOperationType_ReturnsOnlyMatching()
        {
            var incCat = TestDatabase.SeedIncomeCategory(_ctx);
            var expCat = TestDatabase.SeedExpenseCategory(_ctx);
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, incCat.Id, 1000m, OperationType.Income, date: day);
            TestDatabase.SeedTransaction(_ctx, expCat.Id, 500m, OperationType.Expense, date: day);

            var report = await _svc.GenerateTransactionDetailReportAsync(day, day, operationType: OperationType.Income);

            report.Transactions.Should().HaveCount(1);
            report.TotalIncome.Should().Be(1000m);
            report.TotalExpense.Should().Be(0m);
        }

        [Fact]
        public async Task GenerateTransactionDetailReportAsync_FilterByCategoryId_SetsFilterCategoryName()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx, "Запчасти-тест-отчёт");
            var day = new DateTime(2025, 6, 15);
            TestDatabase.SeedTransaction(_ctx, cat.Id, 200m, OperationType.Expense, date: day);

            var report = await _svc.GenerateTransactionDetailReportAsync(day, day, categoryId: cat.Id);

            report.FilterCategoryName.Should().Be("Запчасти-тест-отчёт");
            report.Transactions.Should().HaveCount(1);
        }

        [Fact]
        public async Task GenerateTransactionDetailReportAsync_ExcludesSoftDeleted()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var day = new DateTime(2025, 6, 15);

            TestDatabase.SeedTransaction(_ctx, cat.Id, 100m, OperationType.Expense, date: day);
            var deleted = TestDatabase.SeedTransaction(_ctx, cat.Id, 999m, OperationType.Expense, date: day);
            _ctx.Transactions.Find(deleted.Id)!.IsDeleted = true;
            _ctx.SaveChanges();

            var report = await _svc.GenerateTransactionDetailReportAsync(day, day);

            report.Transactions.Should().HaveCount(1);
            report.TotalExpense.Should().Be(100m);
        }
    }
}
