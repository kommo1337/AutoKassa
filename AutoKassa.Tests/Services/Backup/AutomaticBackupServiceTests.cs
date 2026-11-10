using AutoKassa.Services.Backup;
using AutoKassa.Services.Backup.YandexDisk;
using AutoKassa.Tests.Infrastructure;
using AutoKassa.Tests.Services.Backup.YandexDisk;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services.Backup
{
    public class AutomaticBackupServiceTests
    {
        private readonly FakeSettingsService _settings;
        private readonly FakeYandexDiskBackupService _cloud;
        private readonly AutomaticBackupService _svc;

        public AutomaticBackupServiceTests()
        {
            _settings = new FakeSettingsService();
            _cloud = new FakeYandexDiskBackupService();
            // Короткий интервал тика, чтобы тестировать расписание без часового ожидания.
            _svc = new AutomaticBackupService(_settings, _cloud, TimeProvider.System, TimeSpan.FromMilliseconds(30));
        }

        [Fact]
        public async Task Start_WhenCloudDisabled_RunsLocalCheckOnly()
        {
            _settings.Settings.YandexDiskBackupEnabled = false;

            _svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => _settings.RunAutoBackupIfDueCallCount >= 1);
            await _svc.StopAsync();

            _cloud.UploadCallCount.Should().Be(0, "облачная часть выключена — UploadNowAsync не вызывается");
        }

        [Fact]
        public async Task Start_WhenCloudEnabled_RunsBothChecksWithForceFalse()
        {
            _settings.Settings.YandexDiskBackupEnabled = true;

            _svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => _cloud.UploadCallCount >= 1);
            await _svc.StopAsync();

            _settings.RunAutoBackupIfDueCallCount.Should().BeGreaterThanOrEqualTo(1);
            _cloud.UploadForceArgs.Should().OnlyContain(f => f == false, "плановая проверка не принудительная");
        }

        [Fact]
        public async Task RequestCheck_RunsOutOfScheduleCheck()
        {
            _settings.Settings.YandexDiskBackupEnabled = true;
            _svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => _settings.RunAutoBackupIfDueCallCount >= 1);
            var before = _settings.RunAutoBackupIfDueCallCount;

            _svc.RequestCheck();

            await TestWaitHelper.WaitUntilAsync(() => _settings.RunAutoBackupIfDueCallCount > before);
            await _svc.StopAsync();
        }

        [Fact]
        public async Task RequestCheck_BeforeStart_IsIgnored()
        {
            _svc.RequestCheck();
            await Task.Delay(100);

            _settings.RunAutoBackupIfDueCallCount.Should().Be(0);
        }

        [Fact]
        public async Task TimerTick_AfterFailure_ContinuesSchedule()
        {
            _settings.Settings.YandexDiskBackupEnabled = true;
            var calls = 0;
            _settings.RunAutoBackupIfDueOverride = _ =>
            {
                calls++;
                return calls == 1
                    ? Task.FromException(new InvalidOperationException("симулированный сбой"))
                    : Task.CompletedTask;
            };

            _svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => calls >= 2);
            await _svc.StopAsync();

            calls.Should().BeGreaterThanOrEqualTo(2, "исключение одного тика не останавливает расписание");
        }

        [Fact]
        public async Task CloudFailure_DoesNotStopSchedule()
        {
            _settings.Settings.YandexDiskBackupEnabled = true;
            var uploads = 0;
            _cloud.UploadOverride = (_, _) =>
            {
                uploads++;
                return uploads == 1
                    ? Task.FromException<CloudBackupResult>(new InvalidOperationException("симулированный сбой облака"))
                    : Task.FromResult(new CloudBackupResult(true, _cloud.CurrentState));
            };

            _svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => uploads >= 2);
            await _svc.StopAsync();

            uploads.Should().BeGreaterThanOrEqualTo(2, "сбой облачной части не должен останавливать расписание");
        }

        [Fact]
        public async Task ConcurrentChecks_AreSerialized()
        {
            _settings.Settings.YandexDiskBackupEnabled = true;
            var concurrent = 0;
            var maxConcurrent = 0;
            _cloud.UploadOverride = async (_, _) =>
            {
                var now = Interlocked.Increment(ref concurrent);
                UpdateMax(now);
                await Task.Delay(50);
                Interlocked.Decrement(ref concurrent);
                return new CloudBackupResult(true, _cloud.CurrentState);

                void UpdateMax(int value)
                {
                    int initial;
                    while (value > (initial = Volatile.Read(ref maxConcurrent)))
                        Interlocked.CompareExchange(ref maxConcurrent, value, initial);
                }
            };

            _svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => _cloud.UploadCallCount >= 1);

            _svc.RequestCheck();
            _svc.RequestCheck();
            await TestWaitHelper.WaitUntilAsync(() => _cloud.UploadCallCount >= 3, timeoutMs: 10_000);
            await _svc.StopAsync();

            maxConcurrent.Should().Be(1, "стартовая, таймерная и внеочередная проверки не должны выполняться параллельно");
        }

        [Fact]
        public async Task LocalFailure_DoesNotSkipCloudPartInSameTick()
        {
            // YD-09 (долгосрочное): изолированные части — постоянный локальный сбой
            // не должен откладывать облачную выгрузку до следующего тика.
            _settings.Settings.YandexDiskBackupEnabled = true;
            _settings.RunAutoBackupIfDueOverride = _ =>
                Task.FromException(new InvalidOperationException("постоянный сбой локальной части"));

            _svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => _cloud.UploadCallCount >= 1);
            await _svc.StopAsync();

            _cloud.UploadCallCount.Should().BeGreaterThanOrEqualTo(1,
                "облачная часть выполняется в том же тике, несмотря на локальный сбой");
        }

        [Fact]
        public async Task StopAsync_CancelsActiveOperationAndWaits()
        {
            _settings.Settings.YandexDiskBackupEnabled = true;
            CancellationToken observedToken = default;
            var entered = new TaskCompletionSource();
            _cloud.UploadOverride = (_, ct) =>
            {
                observedToken = ct;
                entered.TrySetResult();
                return Task.FromException<CloudBackupResult>(new OperationCanceledException(ct));
            };

            _svc.Start();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var stopTask = _svc.StopAsync();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(10));

            observedToken.IsCancellationRequested.Should().BeTrue("остановка отменяет активную операцию");
        }

        [Fact]
        public async Task StopAsync_WaitsForInFlightRequestCheck()
        {
            // YD-06: внеочередная проверка отслеживается — StopAsync не завершается, пока она выполняется.
            _settings.Settings.YandexDiskBackupEnabled = true;
            var svc = new AutomaticBackupService(_settings, _cloud, TimeProvider.System, TimeSpan.FromMinutes(5));
            svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => _settings.RunAutoBackupIfDueCallCount >= 1);

            var entered = new TaskCompletionSource();
            var release = new TaskCompletionSource();
            _settings.RunAutoBackupIfDueOverride = _ =>
            {
                entered.TrySetResult();
                return release.Task;
            };

            svc.RequestCheck();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var stopTask = svc.StopAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(300);
            stopTask.IsCompleted.Should().BeFalse("остановка обязана ждать завершения внеочередной проверки");

            release.TrySetResult();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(10));
            stopTask.IsCompleted.Should().BeTrue("после завершения проверки остановка завершается штатно");
        }

        [Fact]
        public async Task RequestCheck_WhileBusy_CoalescesIntoSingleExtraCheck()
        {
            // YD-06 (долгосрочное): повторные запросы во время занятого worker схлопываются в одну ожидающую проверку.
            _settings.Settings.YandexDiskBackupEnabled = true;
            var svc = new AutomaticBackupService(_settings, _cloud, TimeProvider.System, TimeSpan.FromMinutes(5));
            svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => _settings.RunAutoBackupIfDueCallCount >= 1);

            var entered = new TaskCompletionSource();
            var release = new TaskCompletionSource();
            _settings.RunAutoBackupIfDueOverride = _ =>
            {
                entered.TrySetResult();
                return release.Task;
            };

            svc.RequestCheck();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            svc.RequestCheck();
            svc.RequestCheck();
            svc.RequestCheck();
            await Task.Delay(300); // worker занят — запросы должны схлопнуться, а не накапливаться

            release.TrySetResult();
            await TestWaitHelper.WaitUntilAsync(() => _settings.RunAutoBackupIfDueCallCount >= 2);
            await Task.Delay(300); // стабилизация: таймер далёк (5 мин), новых запусков быть не должно

            _settings.RunAutoBackupIfDueCallCount.Should().Be(3,
                "стартовая проверка + заблокированная внеочередная + одна схлопнувшаяся из трёх повторных запросов");
            await svc.StopAsync();
        }

        [Fact]
        public async Task RequestCheck_AfterStop_IsIgnored()
        {
            _settings.Settings.YandexDiskBackupEnabled = true;
            var svc = new AutomaticBackupService(_settings, _cloud, TimeProvider.System, TimeSpan.FromMinutes(5));
            svc.Start();
            await TestWaitHelper.WaitUntilAsync(() => _settings.RunAutoBackupIfDueCallCount >= 1);
            await svc.StopAsync();

            var before = _settings.RunAutoBackupIfDueCallCount;
            svc.RequestCheck();
            await Task.Delay(200);

            _settings.RunAutoBackupIfDueCallCount.Should().Be(before, "после остановки новые внеочередные проверки не запускаются");
        }

        [Fact]
        public async Task StopAsync_WhenNotStarted_IsNoOp()
        {
            await _svc.StopAsync();
            _settings.RunAutoBackupIfDueCallCount.Should().Be(0);
        }
    }
}
