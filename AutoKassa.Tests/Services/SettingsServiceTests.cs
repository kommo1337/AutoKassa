using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class SettingsServiceTests : IDisposable
    {
        private readonly TestDbContextFactory _factory;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly SettingsService _svc;

        public SettingsServiceTests()
        {
            (_factory, _conn) = TestDatabase.CreateWithFactory();
            _svc = new SettingsService(_factory);
        }

        public void Dispose()
        {
            _conn.Dispose();
        }

        [Fact]
        public void Constructor_CreatesDefaultSettings_WhenNoneExist()
        {
            var settings = _svc.GetSettings();

            settings.Should().NotBeNull();
            settings.Id.Should().BeGreaterThan(0);
            settings.Theme.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task SaveSettings_PersistsChanges()
        {
            var settings = _svc.GetSettings();
            settings.AutoLockTimeout = 99;
            await _svc.SaveSettingsAsync(settings);

            // Создаём новый сервис для проверки persistance
            var svc2 = new SettingsService(_factory);
            svc2.GetSettings().AutoLockTimeout.Should().Be(99);
        }

        [Fact]
        public async Task ResetToDefaultsAsync_PreservesPassword()
        {
            var settings = _svc.GetSettings();
            settings.PasswordHash = "preserved-hash";
            await _svc.SaveSettingsAsync(settings);

            await _svc.ResetToDefaultsAsync();

            _svc.GetSettings().PasswordHash.Should().Be("preserved-hash");
        }

        [Fact]
        public async Task ResetToDefaultsAsync_ResetsOtherFields()
        {
            var settings = _svc.GetSettings();
            settings.AutoLockTimeout = 999;
            settings.Theme = "Dark";
            await _svc.SaveSettingsAsync(settings);

            await _svc.ResetToDefaultsAsync();

            var reset = _svc.GetSettings();
            reset.AutoLockTimeout.Should().Be(10);
            reset.Theme.Should().Be("Light");
        }

        [Fact]
        public async Task IsPasswordSet_ReturnsFalse_WhenEmpty()
        {
            var settings = _svc.GetSettings();
            settings.PasswordHash = string.Empty;
            await _svc.SaveSettingsAsync(settings);

            _svc.IsPasswordSet().Should().BeFalse();
        }

        [Fact]
        public async Task IsPasswordSet_ReturnsTrue_WhenHashSet()
        {
            var settings = _svc.GetSettings();
            settings.PasswordHash = "$2a$10$somevalidhash";
            await _svc.SaveSettingsAsync(settings);

            _svc.IsPasswordSet().Should().BeTrue();
        }

        [Fact]
        public async Task SetDefaultCategoryId_Income_SetsCorrectField()
        {
            await _svc.SetDefaultCategoryIdAsync(OperationType.Income, 42);

            (await _svc.GetDefaultCategoryIdAsync(OperationType.Income)).Should().Be(42);
            (await _svc.GetDefaultCategoryIdAsync(OperationType.Expense)).Should().BeNull();
        }

        [Fact]
        public async Task SetDefaultCategoryId_Expense_SetsCorrectField()
        {
            await _svc.SetDefaultCategoryIdAsync(OperationType.Expense, 7);

            (await _svc.GetDefaultCategoryIdAsync(OperationType.Expense)).Should().Be(7);
            (await _svc.GetDefaultCategoryIdAsync(OperationType.Income)).Should().BeNull();
        }

        [Fact]
        public async Task GetSettingsAsync_ReturnsCachedInstance()
        {
            var s1 = await _svc.GetSettingsAsync();
            var s2 = await _svc.GetSettingsAsync();

            s1.Should().BeSameAs(s2);
        }

        [Fact]
        public async Task SetTheme_PersistsAndUpdatesCachedSettings()
        {
            await _svc.SetThemeAsync("Dark");

            _svc.GetTheme().Should().Be("Dark");

            // Проверяем через новый экземпляр
            var svc2 = new SettingsService(_factory);
            svc2.GetTheme().Should().Be("Dark");
        }

        [Fact]
        public async Task ImportSettingsAsync_OutOfRangeValues_AreNormalizedToDefaults()
        {
            // Значения за пределами диапазонов SettingsValidator
            var json = """
                {
                    "AutoLockTimeout": 0,
                    "DefaultPageSize": 5,
                    "AutoBackupDays": 0,
                    "PasswordExpireDays": -1,
                    "BackupKeepCount": 2147483647,
                    "WindowWidth": 100,
                    "WindowHeight": 100,
                    "DefaultOperationType": 99,
                    "DefaultPaymentType": 42,
                    "InitialBalance": -100,
                    "CreditCardLimit": -1,
                    "CreditCardCurrentDebt": -1,
                    "CreditCardInterestRate": -1,
                    "CreditCardPaymentDay": 0,
                    "CreditCardMinimumPaymentPercent": 150,
                    "Theme": "Neon",
                    "BackupFrequency": "Hourly",
                    "Language": "en-US"
                }
                """;

            var result = await ImportJsonAsync(json);

            result.Should().BeTrue();
            var settings = _svc.GetSettings();
            settings.AutoLockTimeout.Should().Be(10);
            settings.DefaultPageSize.Should().Be(20);
            settings.AutoBackupDays.Should().Be(7);
            settings.PasswordExpireDays.Should().Be(0);
            settings.BackupKeepCount.Should().Be(10);
            settings.WindowWidth.Should().Be(1200);
            settings.WindowHeight.Should().Be(700);
            settings.DefaultOperationType.Should().Be((int)OperationType.Expense);
            settings.DefaultPaymentType.Should().Be(1);
            settings.InitialBalance.Should().Be(0m);
            settings.CreditCardLimit.Should().Be(0m);
            settings.CreditCardCurrentDebt.Should().Be(0m);
            settings.CreditCardInterestRate.Should().Be(0m);
            settings.CreditCardPaymentDay.Should().Be(10);
            settings.CreditCardMinimumPaymentPercent.Should().Be(5m);
            settings.Theme.Should().Be("Light");
            settings.BackupFrequency.Should().Be("Weekly");
            settings.Language.Should().Be("ru-RU");
        }

        [Fact]
        public async Task ImportSettingsAsync_ValidValues_AreAppliedAsIs()
        {
            var json = """
                {
                    "AutoLockTimeout": 30,
                    "DefaultPageSize": 50,
                    "BackupKeepCount": 25,
                    "CreditCardPaymentDay": 20,
                    "InitialBalance": 1500.5,
                    "Theme": "Dark",
                    "BackupFrequency": "Daily"
                }
                """;

            var result = await ImportJsonAsync(json);

            result.Should().BeTrue();
            var settings = _svc.GetSettings();
            settings.AutoLockTimeout.Should().Be(30);
            settings.DefaultPageSize.Should().Be(50);
            settings.BackupKeepCount.Should().Be(25);
            settings.CreditCardPaymentDay.Should().Be(20);
            settings.InitialBalance.Should().Be(1500.5m);
            settings.Theme.Should().Be("Dark");
            settings.BackupFrequency.Should().Be("Daily");
        }

        /// <summary>
        /// Записать JSON во временный файл и импортировать его через сервис.
        /// </summary>
        private async Task<bool> ImportJsonAsync(string json)
        {
            var path = Path.Combine(Path.GetTempPath(), $"autokassa_settings_{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(path, json);
            try
            {
                return await _svc.ImportSettingsAsync(path);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
