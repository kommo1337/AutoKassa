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
        public void SaveSettings_PersistsChanges()
        {
            var settings = _svc.GetSettings();
            settings.AutoLockTimeout = 99;
            _svc.SaveSettings(settings);

            // Создаём новый сервис для проверки persistance
            var svc2 = new SettingsService(_factory);
            svc2.GetSettings().AutoLockTimeout.Should().Be(99);
        }

        [Fact]
        public async Task ResetToDefaultsAsync_PreservesPassword()
        {
            var settings = _svc.GetSettings();
            settings.PasswordHash = "preserved-hash";
            _svc.SaveSettings(settings);

            await _svc.ResetToDefaultsAsync();

            _svc.GetSettings().PasswordHash.Should().Be("preserved-hash");
        }

        [Fact]
        public async Task ResetToDefaultsAsync_ResetsOtherFields()
        {
            var settings = _svc.GetSettings();
            settings.AutoLockTimeout = 999;
            settings.Theme = "Dark";
            _svc.SaveSettings(settings);

            await _svc.ResetToDefaultsAsync();

            var reset = _svc.GetSettings();
            reset.AutoLockTimeout.Should().Be(10);
            reset.Theme.Should().Be("Light");
        }

        [Fact]
        public void IsPasswordSet_ReturnsFalse_WhenEmpty()
        {
            var settings = _svc.GetSettings();
            settings.PasswordHash = string.Empty;
            _svc.SaveSettings(settings);

            _svc.IsPasswordSet().Should().BeFalse();
        }

        [Fact]
        public void IsPasswordSet_ReturnsTrue_WhenHashSet()
        {
            var settings = _svc.GetSettings();
            settings.PasswordHash = "$2a$10$somevalidhash";
            _svc.SaveSettings(settings);

            _svc.IsPasswordSet().Should().BeTrue();
        }

        [Fact]
        public void SetDefaultCategoryId_Income_SetsCorrectField()
        {
            _svc.SetDefaultCategoryId(OperationType.Income, 42);

            _svc.GetDefaultCategoryId(OperationType.Income).Should().Be(42);
            _svc.GetDefaultCategoryId(OperationType.Expense).Should().BeNull();
        }

        [Fact]
        public void SetDefaultCategoryId_Expense_SetsCorrectField()
        {
            _svc.SetDefaultCategoryId(OperationType.Expense, 7);

            _svc.GetDefaultCategoryId(OperationType.Expense).Should().Be(7);
            _svc.GetDefaultCategoryId(OperationType.Income).Should().BeNull();
        }

        [Fact]
        public async Task GetSettingsAsync_ReturnsCachedInstance()
        {
            var s1 = await _svc.GetSettingsAsync();
            var s2 = await _svc.GetSettingsAsync();

            s1.Should().BeSameAs(s2);
        }

        [Fact]
        public void SetTheme_PersistsAndUpdatesCachedSettings()
        {
            _svc.SetTheme("Dark");

            _svc.GetTheme().Should().Be("Dark");

            // Проверяем через новый экземпляр
            var svc2 = new SettingsService(_factory);
            svc2.GetTheme().Should().Be("Dark");
        }
    }
}
