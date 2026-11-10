using AutoKassa.Services.Backup.YandexDisk;
using Serilog;

namespace AutoKassa.Services.Backup
{
    /// <summary>
    /// Реализация координатора расписания резервного копирования.
    /// Локальные и облачные сроки проверяются независимо: локальные правила остаются
    /// внутри <see cref="ISettingsService.RunAutoBackupIfDueAsync"/>, облачный флаг
    /// (<c>YandexDiskBackupEnabled</c>) проверяет здесь, а срок и очередь — внутри
    /// <see cref="IYandexDiskBackupService.UploadNowAsync"/>. Все запуски сериализуются
    /// через SemaphoreSlim; внеочередные проверки обрабатывает один worker с coalescing
    /// (YD-06), а StopAsync ожидает и таймерный цикл, и worker в пределах общего таймаута.
    /// </summary>
    public sealed class AutomaticBackupService : IAutomaticBackupService
    {
        private static readonly ILogger _log = Log.ForContext<AutomaticBackupService>();

        // Период проверки сроков локальной и облачной копий.
        public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

        // Ограниченное ожидание завершения активной операции при выходе из приложения.
        private static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(10);

        private readonly ISettingsService _settingsService;
        private readonly IYandexDiskBackupService _cloudBackupService;
        private readonly TimeProvider _time;
        private readonly TimeSpan _checkInterval;

        private readonly SemaphoreSlim _runGate = new(1, 1);
        private readonly CancellationTokenSource _cts = new();

        // Внеочередные проверки обрабатывает один отслеживаемый worker (YD-06):
        // пока worker занят, повторные RequestCheck схлопываются в одну ожидающую проверку.
        private readonly object _requestSync = new();
        private bool _checkPending;
        private Task? _checkWorker;

        private Task? _timerLoop;
        private int _started;
        private int _stopping;

        public AutomaticBackupService(
            ISettingsService settingsService,
            IYandexDiskBackupService cloudBackupService,
            TimeProvider? time = null,
            TimeSpan? checkInterval = null)
        {
            _settingsService = settingsService;
            _cloudBackupService = cloudBackupService;
            _time = time ?? TimeProvider.System;
            _checkInterval = checkInterval ?? CheckInterval;
        }

        /// <inheritdoc />
        public void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
                return;

            _timerLoop = Task.Run(TimerLoopAsync);
            _log.Information("Координатор автобэкапа запущен (проверка каждые {Interval})", _checkInterval);
        }

        /// <inheritdoc />
        public void RequestCheck()
        {
            if (Volatile.Read(ref _started) == 0 || Volatile.Read(ref _stopping) == 1)
                return;

            lock (_requestSync)
            {
                _checkPending = true;
                _checkWorker ??= Task.Run(RequestWorkerLoopAsync);
            }
        }

        /// <inheritdoc />
        public async Task StopAsync(TimeSpan? timeout = null)
        {
            if (Volatile.Read(ref _started) == 0)
                return;
            if (Interlocked.Exchange(ref _stopping, 1) != 0)
                return; // остановка уже запущена — ждать должен первый вызов

            _cts.Cancel();

            var wait = timeout ?? DefaultStopTimeout;
            var tasks = new List<Task>();
            if (_timerLoop != null)
                tasks.Add(_timerLoop);
            lock (_requestSync)
            {
                if (_checkWorker != null)
                    tasks.Add(_checkWorker);
            }
            if (tasks.Count == 0)
                return;

            try
            {
                var all = Task.WhenAll(tasks);
                var completed = await Task.WhenAny(all, Task.Delay(wait, _time)).ConfigureAwait(false);
                if (completed != all)
                    _log.Warning("Активная операция резервного копирования не завершилась за {Timeout} — завершаем приложение", wait);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Ошибка ожидания остановки координатора автобэкапа");
            }
        }

        /// <summary>
        /// Единственный worker внеочередных проверок: забирает ожидающий запрос и выполняет
        /// его под общей блокировкой запусков; пришедшие во время работы запросы схлопываются
        /// в одну повторную проверку. Завершается, когда запросов больше нет или сервис останавливается.
        /// </summary>
        private async Task RequestWorkerLoopAsync()
        {
            try
            {
                while (true)
                {
                    lock (_requestSync)
                    {
                        if (_checkPending && Volatile.Read(ref _stopping) == 0)
                        {
                            _checkPending = false;
                        }
                        else
                        {
                            // Выход только когда нет ожидающих запросов — в этой точке проверка
                            // не выполняется, поэтому обнулить ссылку безопасно (StopAsync не потеряет её).
                            if (Volatile.Read(ref _stopping) == 1)
                                _checkPending = false;
                            _checkWorker = null;
                            return;
                        }
                    }

                    await RunCheckOnceAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                // Страховка от неожиданного исключения: ссылку держим, пока цикл реально жив.
                lock (_requestSync)
                {
                    _checkWorker = null;
                }
            }
        }

        /// <summary>Цикл: немедленная проверка при старте, затем тики таймера.</summary>
        private async Task TimerLoopAsync()
        {
            try
            {
                await RunCheckOnceAsync().ConfigureAwait(false);

                using var timer = new PeriodicTimer(_checkInterval, _time);
                while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
                {
                    await RunCheckOnceAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Штатная остановка при выходе из приложения.
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Цикл координатора автобэкапа завершился с ошибкой");
            }
        }

        /// <summary>Одна проверка: локальная часть + облачная часть (если включена).
        /// Части изолированы: сбой локальной не пропускает независимую облачную (YD-09).</summary>
        private async Task RunCheckOnceAsync()
        {
            try
            {
                await _runGate.WaitAsync(_cts.Token).ConfigureAwait(false);
                try
                {
                    await RunLocalPartAsync().ConfigureAwait(false);
                    await RunCloudPartAsync().ConfigureAwait(false);
                }
                finally
                {
                    _runGate.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Отмена при выходе из приложения — штатная ситуация.
            }
            catch (Exception ex)
            {
                // Один сбой не должен останавливать расписание — ждём следующего тика.
                _log.Error(ex, "Плановая проверка резервного копирования не выполнена");
            }
        }

        /// <summary>Локальная часть: правила (BackupEnabled, срок) внутри метода — не дублируем.</summary>
        private async Task RunLocalPartAsync()
        {
            try
            {
                await _settingsService.RunAutoBackupIfDueAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Локальный сбой не должен пропускать облачную часть того же тика.
                _log.Error(ex, "Локальная часть проверки резервного копирования не выполнена");
            }
        }

        /// <summary>Облачная часть: флаг общего включения проверяет координатор,
        /// срок и наличие файла в очереди — внутри облачного сервиса.</summary>
        private async Task RunCloudPartAsync()
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);
                if (settings.YandexDiskBackupEnabled)
                {
                    await _cloudBackupService.UploadNowAsync(force: false, ct: _cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Облачная часть проверки резервного копирования не выполнена");
            }
        }
    }
}
