using System.Reflection;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using AutoKassa.ViewModels;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.ViewModels
{
    public class MainWindowViewModelTests : IDisposable
    {
        private readonly TestDbContextFactory _factory;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly SettingsService _settingsService;
        private readonly FakeNavigationService _navigationService;
        private readonly FakeLockService _lockService;

        public MainWindowViewModelTests()
        {
            (_factory, _conn) = TestDatabase.CreateWithFactory();
            _settingsService = new SettingsService(_factory);
            _navigationService = new FakeNavigationService();
            _lockService = new FakeLockService();
        }

        public void Dispose()
        {
            _conn.Dispose();
        }

        private MainWindowViewModel CreateViewModel() =>
            new MainWindowViewModel(_navigationService, _lockService, _settingsService);

        [Fact]
        public void Constructor_StartsAutoLockTimer()
        {
            using var vm = CreateViewModel();

            _lockService.StartCount.Should().Be(1);
        }

        [Fact]
        public void NotifyUserActivity_FirstCall_ResetsAutoLockTimer()
        {
            using var vm = CreateViewModel();

            vm.NotifyUserActivity();

            _lockService.ResetCount.Should().Be(1);
        }

        [Fact]
        public void NotifyUserActivity_WithinThrottleInterval_Ignored()
        {
            using var vm = CreateViewModel();

            vm.NotifyUserActivity();
            vm.NotifyUserActivity();
            vm.NotifyUserActivity();

            _lockService.ResetCount.Should().Be(1);
        }

        [Fact]
        public void NotifyUserActivity_AfterThrottleInterval_ResetsAgain()
        {
            using var vm = CreateViewModel();
            vm.NotifyUserActivity();

            // «Проматываем» время последнего сброса за пределы интервала троттлинга (15 с)
            typeof(MainWindowViewModel)
                .GetField("_lastActivityReset", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(vm, DateTime.MinValue);

            vm.NotifyUserActivity();

            _lockService.ResetCount.Should().Be(2);
        }
    }
}
