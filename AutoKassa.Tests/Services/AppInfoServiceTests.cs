using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Services.Settings;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class AppInfoServiceTests : IDisposable
    {
        private readonly TestDbContextFactory _factory;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly ISettingsService _settingsService;
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly AppInfoService _svc;

        public AppInfoServiceTests()
        {
            (_factory, _conn) = TestDatabase.CreateWithFactory();
            _settingsService = new SettingsService(_factory);
            _transactionService = new TransactionService(_factory);
            _categoryService = new CategoryService(_factory, new ReferenceDataCache(_factory, new DataChangeService()));
            _svc = new AppInfoService(_settingsService, _transactionService, _categoryService);
        }

        public void Dispose()
        {
            _conn.Dispose();
        }

        [Fact]
        public async Task GetAppInfoAsync_ReturnsVersionAndCounts()
        {
            var info = await _svc.GetAppInfoAsync();

            info.Should().NotBeNull();
            info.AppVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
            info.DbFileSize.Should().NotBeNullOrEmpty();
            info.TotalTransactions.Should().Be(0);
            info.TotalCategories.Should().BeGreaterThan(0);
            info.LastBackupDate.Should().Be("Нет данных");
        }

        [Fact]
        public async Task GetAppInfoAsync_CountsTransactionsAndCategories()
        {
            using var ctx = _factory.CreateDbContext();
            var category = TestDatabase.SeedExpenseCategory(ctx, "Тестовая категория");
            TestDatabase.SeedTransaction(ctx, category.Id, 1000m, OperationType.Expense, PaymentType.Cash);
            TestDatabase.SeedTransaction(ctx, category.Id, 2000m, OperationType.Expense, PaymentType.Cash);

            var info = await _svc.GetAppInfoAsync();

            info.TotalTransactions.Should().Be(2);
            info.TotalCategories.Should().BeGreaterThan(1);
        }

        [Fact]
        public async Task GetAppInfoAsync_LastBackupDate_FormattedCorrectly()
        {
            var settings = _settingsService.GetSettings();
            var backupDir = Path.Combine(Path.GetTempPath(), $"AutoKassaTestBackup_{Guid.NewGuid()}");
            Directory.CreateDirectory(backupDir);
            try
            {
                settings.BackupPath = backupDir;
                await _settingsService.SaveSettingsAsync(settings);

                // In-memory БД не поддерживает Online Backup API, поэтому имитируем файл бэкапа.
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fakeBackup = Path.Combine(backupDir, $"AutoKassa_Backup_{timestamp}.db");
                File.WriteAllText(fakeBackup, "fake");

                var info = await _svc.GetAppInfoAsync();
                info.LastBackupDate.Should().MatchRegex(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$");
            }
            finally
            {
                try { Directory.Delete(backupDir, recursive: true); }
                catch { /* игнорируем ошибки очистки */ }
            }
        }
    }
}
