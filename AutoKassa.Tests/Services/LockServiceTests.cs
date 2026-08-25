using System.Reflection;
using System.Windows.Threading;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoKassa.Tests.Services
{
    /// <summary>
    /// Тесты логики таймера автоблокировки. Пути Lock()/Shutdown не покрываются:
    /// они требуют запущенного WPF Application.
    /// </summary>
    public class LockServiceTests : IDisposable
    {
        private readonly TestDbContextFactory _factory;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly SettingsService _settingsService;
        private readonly LockService _svc;

        public LockServiceTests()
        {
            (_factory, _conn) = TestDatabase.CreateWithFactory();
            _settingsService = new SettingsService(_factory);
            _svc = new LockService(
                new ServiceCollection().BuildServiceProvider(),
                new FakeDialogService(),
                new PasswordService(),
                _settingsService);
        }

        public void Dispose()
        {
            _svc.Dispose();
            _conn.Dispose();
        }

        private async Task SetAutoLock(bool enabled, int timeoutMinutes)
        {
            var settings = _settingsService.GetSettings();
            settings.AutoLockEnabled = enabled;
            settings.AutoLockTimeout = timeoutMinutes;
            await _settingsService.SaveSettingsAsync(settings);
        }

        private static DispatcherTimer? GetTimer(LockService svc) =>
            (DispatcherTimer?)typeof(LockService)
                .GetField("_autoLockTimer", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(svc);

        [Fact]
        public async Task StartAutoLockTimer_DisabledInSettings_TimerNotCreated()
        {
            await SetAutoLock(enabled: false, timeoutMinutes: 5);

            _svc.StartAutoLockTimer();

            GetTimer(_svc).Should().BeNull();
        }

        [Fact]
        public async Task StartAutoLockTimer_ZeroTimeout_TimerNotCreated()
        {
            await SetAutoLock(enabled: true, timeoutMinutes: 0);

            _svc.StartAutoLockTimer();

            GetTimer(_svc).Should().BeNull();
        }

        [Fact]
        public async Task StartAutoLockTimer_Enabled_CreatesTimerWithInterval()
        {
            await SetAutoLock(enabled: true, timeoutMinutes: 5);

            _svc.StartAutoLockTimer();

            var timer = GetTimer(_svc);
            timer.Should().NotBeNull();
            timer!.IsEnabled.Should().BeTrue();
            timer.Interval.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Fact]
        public async Task StopAutoLockTimer_StopsTimer()
        {
            await SetAutoLock(enabled: true, timeoutMinutes: 5);
            _svc.StartAutoLockTimer();

            _svc.StopAutoLockTimer();

            GetTimer(_svc)!.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public async Task ResetAutoLockTimer_WhenTimerNeverStarted_DoesNothing()
        {
            await SetAutoLock(enabled: true, timeoutMinutes: 5);

            _svc.ResetAutoLockTimer();

            GetTimer(_svc).Should().BeNull();
        }

        [Fact]
        public async Task ResetAutoLockTimer_AfterStop_RestartsTimer()
        {
            await SetAutoLock(enabled: true, timeoutMinutes: 5);
            _svc.StartAutoLockTimer();
            _svc.StopAutoLockTimer();

            _svc.ResetAutoLockTimer();

            GetTimer(_svc)!.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public async Task StartAutoLockTimer_AfterDisablingInSettings_StopsExistingTimer()
        {
            await SetAutoLock(enabled: true, timeoutMinutes: 5);
            _svc.StartAutoLockTimer();

            await SetAutoLock(enabled: false, timeoutMinutes: 5);
            _svc.StartAutoLockTimer();

            var timer = GetTimer(_svc);
            timer.Should().NotBeNull();
            timer!.IsEnabled.Should().BeFalse();
        }
    }
}
