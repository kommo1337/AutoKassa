using AutoKassa.Models.Enums;
using Microsoft.Data.Sqlite;
using Serilog;
using System.IO;
using System.Security.Cryptography;

namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Реализация выгрузки резервных копий на Яндекс Диск.
    /// Запуски сериализуются через SemaphoreSlim; технические детали ошибок уходят в лог,
    /// пользователю остаётся только понятное сообщение в состоянии.
    /// </summary>
    public sealed class YandexDiskBackupService : IYandexDiskBackupService
    {
        private static readonly ILogger _log = Log.ForContext<YandexDiskBackupService>();

        // Управляемое имя облачных копий: только такие файлы создаём и удаляем в app:/.
        private const string BackupFileSearchPattern = "AutoKassa_Backup_*.db";
        private const string BackupFilePrefix = "AutoKassa_Backup_";

        // Временная папка внутри очереди: снимок создаётся здесь и переходит в очередь
        // атомарным переименованием только после проверки целостности.
        private const string StagingDirectoryName = ".staging";

        // Политика повторов временных сбоев (сеть, HTTP 429/5xx).
        private const int MaxTransientAttempts = 3;
        private static readonly TimeSpan TransientRetryBaseDelay = TimeSpan.FromSeconds(1);

        // Ожидание появления ресурса на Диске после загрузки (ответы 201 и 202).
        private const int ResourcePollAttempts = 10;
        private static readonly TimeSpan ResourcePollDelay = TimeSpan.FromSeconds(1);

        private readonly ISettingsService _settingsService;
        private readonly IYandexDiskAuthService _authService;
        private readonly IYandexDiskApiClient _apiClient;
        private readonly TimeProvider _time;
        private readonly string _queueDirectory;

        private readonly SemaphoreSlim _gate = new(1, 1);

        // Дата последнего успеха — зеркало AppSettings.YandexDiskLastBackupUtc: перечитывается из
        // настроек в начале каждого запуска и сохраняется через SetYandexDiskLastBackupUtcAsync
        // после подтверждённой выгрузки (переживает перезапуск приложения).
        private DateTime? _lastSuccessUtc;
        private DateTime? _lastFailureUtc;
        private CloudBackupState _state = new(CloudBackupStatus.NotConnected, null, false, "Яндекс Диск не подключён.");

        public YandexDiskBackupService(
            ISettingsService settingsService,
            IYandexDiskAuthService authService,
            IYandexDiskApiClient apiClient,
            TimeProvider? time = null,
            string? queueDirectory = null)
        {
            _settingsService = settingsService;
            _authService = authService;
            _apiClient = apiClient;
            _time = time ?? TimeProvider.System;
            _queueDirectory = queueDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoKassa", "CloudBackupQueue");
        }

        /// <inheritdoc />
        public CloudBackupState CurrentState => _state;

        /// <inheritdoc />
        public event Action<CloudBackupState>? StateChanged;

        /// <inheritdoc />
        public async Task<CloudBackupResult> UploadNowAsync(bool force = false, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await RunCoreAsync(force, ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task<CloudBackupResult> DiscardPendingAndUploadAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var pending = FindNewestQueueFile();
                if (pending != null)
                {
                    // Только производный файл очереди — исходная БД не затрагивается.
                    DeleteQueueFile(pending);
                    _log.Information("Ожидающая облачная копия удалена по команде пользователя: {PendingFile}", pending);
                }

                return await RunCoreAsync(force: true, ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<CloudBackupResult> RunCoreAsync(bool force, CancellationToken ct)
        {
            // Дата последнего успеха персистентна: синхронизируем зеркало из настроек
            // перед каждым запуском, чтобы срок считался от фактической даты выгрузки.
            var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);
            _lastSuccessUtc = settings.YandexDiskLastBackupUtc;

            SetState(CloudBackupStatus.Uploading, "Выгрузка копии на Яндекс Диск…");

            // 1. Без действительного токена ни снимок, ни сетевые вызовы не выполняем.
            var token = await _authService.GetValidAccessTokenAsync(ct).ConfigureAwait(false);
            if (token == null)
            {
                var authState = await _authService.GetStateAsync(ct).ConfigureAwait(false);
                if (authState == YandexDiskAuthState.NotConnected)
                {
                    var notConnected = SetState(CloudBackupStatus.NotConnected, "Яндекс Диск не подключён.");
                    return new CloudBackupResult(true, notConnected);
                }

                var authRequired = SetState(CloudBackupStatus.AuthRequired,
                    "Требуется повторный вход в Яндекс Диск. Подключите аккаунт заново.");
                return new CloudBackupResult(false, authRequired);
            }

            // Поколение подключения на старте: если оно изменится посреди выгрузки
            // (отключение или новый вход), операция не заденет уже сохранённый токен (раздел 6 аудита).
            var generation = _authService.ConnectionGeneration;

            try
            {
                Directory.CreateDirectory(_queueDirectory);

                // 2. Незагруженный файл из очереди переиспользуется независимо от срока;
                //    свежий снимок создаётся во временной папке и попадает в очередь только
                //    после проверки целостности (YD-04).
                var queueFile = FindNewestQueueFile();
                if (queueFile != null)
                {
                    SetState(CloudBackupStatus.PendingRetry, "Повторная выгрузка сохранённой копии на Яндекс Диск…");

                    // Повреждённый файл очереди навсегда блокировал бы новые копии — отправляем в карантин.
                    if (!await TryVerifyLocalSnapshotAsync(queueFile, ct).ConfigureAwait(false))
                    {
                        QuarantineCorruptQueueFile(queueFile);
                        var corrupt = Fail(CloudBackupStatus.Error,
                            "Сохранённая копия повреждена и отправлена в карантин. При следующей попытке будет создана новая копия.",
                            null);
                        return new CloudBackupResult(false, corrupt);
                    }
                }
                else
                {
                    if (!force && !IsDue(settings.AutoBackupDays))
                    {
                        var upToDate = SetState(CloudBackupStatus.UpToDate, BuildUpToDateMessage());
                        return new CloudBackupResult(true, upToDate);
                    }

                    queueFile = await CreateVerifiedSnapshotAsync(ct).ConfigureAwait(false);
                    PruneQueueToNewest();
                    if (queueFile == null || !File.Exists(queueFile))
                    {
                        var error = Fail(CloudBackupStatus.Error,
                            "Не удалось создать резервную копию для выгрузки. Попробуйте ещё раз.",
                            null);
                        return new CloudBackupResult(false, error);
                    }
                }

                var fileName = Path.GetFileName(queueFile);
                var remotePath = "app:/" + fileName;
                var fileSize = new FileInfo(queueFile).Length;
                var fileMd5 = await ComputeMd5Async(queueFile, ct).ConfigureAwait(false);

                // 4–7. Загрузка с ограниченными повторами; 409 и 401 — отдельные ветки.
                var outcome = await UploadWithRetriesAsync(queueFile, remotePath, fileSize, fileMd5, generation, ct).ConfigureAwait(false);
                if (outcome != null)
                    return outcome; // терминальное состояние уже установлено

                // 8. Подтверждённый успех: убираем файл очереди, фиксируем дату, ротируем облако.
                return await CompleteSuccessAsync(queueFile, generation, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Штатная отмена (закрытие приложения) — оставляем честное состояние очереди.
                _log.Information("Облачная выгрузка отменена");
                var hasPending = FindNewestQueueFile() != null;
                _state = new CloudBackupState(
                    hasPending ? CloudBackupStatus.PendingRetry : _state.Status,
                    _lastSuccessUtc,
                    hasPending,
                    hasPending ? "Выгрузка прервана — копия осталась в очереди." : _state.UserMessage,
                    BuildPendingFileInfo(),
                    _lastFailureUtc);
                StateChanged?.Invoke(_state);
                throw;
            }
            catch (Exception ex)
            {
                var error = Fail(CloudBackupStatus.Error,
                    "Не удалось загрузить копию на Яндекс Диск. Попробуйте ещё раз.",
                    ex);
                return new CloudBackupResult(false, error);
            }
        }

        /// <summary>Цикл загрузки. Возвращает не-null результат для терминальных исходов; null = успех.</summary>
        private async Task<CloudBackupResult?> UploadWithRetriesAsync(
            string queueFile, string remotePath, long fileSize, string fileMd5, long generation, CancellationToken ct)
        {
            for (var attempt = 1; attempt <= MaxTransientAttempts; attempt++)
            {
                try
                {
                    var (exists, verified) = await TryUploadAndVerifyAsync(remotePath, queueFile, fileSize, fileMd5, ct)
                        .ConfigureAwait(false);
                    if (!exists)
                    {
                        var conflict = Fail(CloudBackupStatus.Error,
                            "На Яндекс Диске уже есть другой файл с таким именем. Копия не отправлена.",
                            null);
                        return new CloudBackupResult(false, conflict);
                    }

                    if (verified)
                        return null; // успех

                    var mismatch = Fail(CloudBackupStatus.Error,
                        "Яндекс Диск принял файл, но проверка целостности не сошлась. Попробуйте ещё раз.",
                        null);
                    return new CloudBackupResult(false, mismatch);
                }
                catch (YandexDiskAuthException ex)
                {
                    await HandleAuthFailureAsync(generation, ct).ConfigureAwait(false);
                    var state = Fail(CloudBackupStatus.AuthRequired,
                        "Доступ к Яндекс Диску утерян. Подключите аккаунт заново.", ex);
                    return new CloudBackupResult(false, state);
                }
                catch (YandexDiskQuotaException ex)
                {
                    // 403/507: повторять бессмысленно, пока пользователь не разберётся с местом/правами.
                    var state = Fail(CloudBackupStatus.Error,
                        "Недостаточно места или прав на Яндекс Диске. Проверьте Диск и повторите попытку.", ex);
                    return new CloudBackupResult(false, state);
                }
                catch (YandexDiskTransientException ex)
                {
                    if (attempt >= MaxTransientAttempts)
                    {
                        _log.Warning(ex, "Временные сбои облачной выгрузки исчерпаны (попыток: {Max})", MaxTransientAttempts);
                        break;
                    }

                    var delay = ex.RetryAfter ?? TimeSpan.FromSeconds(TransientRetryBaseDelay.TotalSeconds * (1 << (attempt - 1)));
                    _log.Warning(ex, "Временный сбой облачной выгрузки (попытка {Attempt} из {Max}), повтор через {Delay}",
                        attempt, MaxTransientAttempts, delay);
                    SetState(CloudBackupStatus.PendingRetry,
                        $"Сбой связи с Яндекс Диском, повторная попытка {attempt + 1}…");
                    await Task.Delay(delay, _time, ct).ConfigureAwait(false);
                }
            }

            // Повторы исчерпаны — файл остаётся в очереди для следующего запуска.
            _lastFailureUtc = _time.GetUtcNow().UtcDateTime;
            var pending = SetState(CloudBackupStatus.PendingRetry,
                "Не удалось выгрузить копию на Яндекс Диск. Повторная попытка будет выполнена позже.");
            return new CloudBackupResult(false, pending);
        }

        /// <summary>
        /// Одна попытка: ссылка → PUT → ожидание ресурса → сверка размера и MD5.
        /// (exists=false) — 409 с чужим содержимым; иначе true + verified = совпали размер/MD5.
        /// </summary>
        private async Task<(bool exists, bool verified)> TryUploadAndVerifyAsync(
            string remotePath, string queueFile, long fileSize, string fileMd5, CancellationToken ct)
        {
            YandexDiskUploadResult uploadResult;
            try
            {
                var link = await _apiClient.GetUploadLinkAsync(remotePath, overwrite: false, ct).ConfigureAwait(false);
                uploadResult = await _apiClient.UploadFileAsync(link.Href, queueFile, ct).ConfigureAwait(false);
            }
            catch (YandexDiskConflictException)
            {
                // Ответ потерян, а файл уже загрузился ранее — сверяем метаданные существующего ресурса.
                var existing = await GetResourceIfPresentAsync(remotePath, ct).ConfigureAwait(false);
                return (true, existing != null && IsSameFile(existing, fileSize, fileMd5));
            }

            // После 201 и 202 ресурс может появиться на Диске не мгновенно — ждём появления.
            // Только YandexDiskNotFoundException означает «ещё не появился»; Auth/Transient/Quota
            // пробрасываются в retry-обработку, а не превращаются в «проверка целостности не сошлась».
            for (var i = 0; i < ResourcePollAttempts; i++)
            {
                // После 202 Диск просит паузу перед первой проверкой; после 201 пробуем немедленно.
                if (i > 0 || uploadResult == YandexDiskUploadResult.Accepted)
                    await Task.Delay(ResourcePollDelay, _time, ct).ConfigureAwait(false);

                try
                {
                    var polled = await _apiClient.GetResourceAsync(remotePath, ct).ConfigureAwait(false);
                    return (true, IsSameFile(polled, fileSize, fileMd5));
                }
                catch (YandexDiskNotFoundException)
                {
                    // Ресурс ещё не появился — следующая попытка ожидания.
                }
            }

            _log.Warning("Ресурс {RemotePath} не появился на Яндекс Диске после ожидания", remotePath);
            return (true, false);
        }

        /// <summary>
        /// Метаданные ресурса для идемпотентного 409: null только когда ресурса нет (404).
        /// Остальные ошибки (Auth/Transient/Quota) пробрасываются в retry-обработку.
        /// </summary>
        private async Task<DiskResourceInfo?> GetResourceIfPresentAsync(string remotePath, CancellationToken ct)
        {
            try
            {
                return await _apiClient.GetResourceAsync(remotePath, ct).ConfigureAwait(false);
            }
            catch (YandexDiskNotFoundException)
            {
                return null;
            }
        }

        private async Task<CloudBackupResult> CompleteSuccessAsync(string queueFile, long generation, CancellationToken ct)
        {
            // Подключение изменилось, пока шла выгрузка (отключение или новый вход другого аккаунта):
            // чужой успех не фиксируем — файл остаётся в очереди, следующий запуск подтвердит тот же
            // облачный ресурс идемпотентным 409 без повторной передачи данных (раздел 6 аудита).
            if (_authService.ConnectionGeneration != generation)
            {
                _log.Information("Подключение к Яндекс Диску изменено во время выгрузки — успех не фиксируем, копия остаётся в очереди");
                var superseded = SetState(CloudBackupStatus.PendingRetry,
                    "Подключение к Яндекс Диску изменилось во время выгрузки. Копия осталась в очереди и будет подтверждена при следующей попытке.");
                return new CloudBackupResult(false, superseded);
            }

            // Сначала фиксируем дату успеха (раздел 5 аудита): если запись даты не удалась,
            // файл очереди остаётся — следующий запуск подтвердит тот же облачный ресурс
            // идемпотентным 409 без повторной передачи данных.
            _lastSuccessUtc = _time.GetUtcNow().UtcDateTime;

            var dateSaved = true;
            try
            {
                await _settingsService.SetYandexDiskLastBackupUtcAsync(_lastSuccessUtc, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                dateSaved = false;
                _log.Warning(ex, "Дата последней облачной копии не сохранена в настройках");
            }

            if (dateSaved)
            {
                DeleteQueueFile(queueFile);
            }
            else
            {
                _log.Information("Файл очереди сохранён для идемпотентного подтверждения облачной копии");
            }

            // Ротация — лучшее усилие: ошибка удаления старых копий не отменяет подтверждённый успех.
            try
            {
                var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);
                await RotateCloudBackupsAsync(settings.BackupKeepCount, ct).ConfigureAwait(false);
            }
            catch (YandexDiskAuthException ex)
            {
                // Единая обработка 401 на всех стадиях: токен инвалидируем (если подключение не менялось),
                // успех загрузки сохраняем — копия на Диске, дата зафиксирована.
                _log.Warning(ex, "Доступ к Яндекс Диску утерян при ротации облачных копий");
                await HandleAuthFailureAsync(generation, ct).ConfigureAwait(false);
                var authState = SetState(CloudBackupStatus.AuthRequired,
                    "Копия выгружена, но доступ к Яндекс Диску утерян при очистке старых версий. Подключите аккаунт заново.");
                return new CloudBackupResult(true, authState);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Warning(ex, "Ротация облачных копий не выполнена — загруженная копия сохранена");
            }

            var state = SetState(CloudBackupStatus.UpToDate, BuildUpToDateMessage());
            _log.Information("Облачная копия выгружена на Яндекс Диск (последний успех {LastSuccessUtc})", _lastSuccessUtc);
            return new CloudBackupResult(true, state);
        }

        /// <summary>
        /// Единая обработка HTTP 401: инвалидируем сохранённый токен, только если подключение
        /// не изменилось с начала операции — иначе старая выгрузка сотрёт вновь сохранённый токен.
        /// </summary>
        private async Task HandleAuthFailureAsync(long generation, CancellationToken ct)
        {
            if (_authService.ConnectionGeneration != generation)
            {
                _log.Information("HTTP 401 при облачной операции игнорируется: подключение уже изменено (новый вход или отключение)");
                return;
            }

            await _authService.InvalidateTokenAsync(ct).ConfigureAwait(false);
        }

        private async Task RotateCloudBackupsAsync(int keepCount, CancellationToken ct)
        {
            if (keepCount <= 0)
                return;

            var resources = await _apiClient.GetAppFolderResourcesAsync(ct).ConfigureAwait(false);
            // Удаляем только файлы с точным управляемым именем копии: папка или файл с «похожим»
            // именем в пользовательской зоне app:/ не считаются нашими резервными копиями.
            var managed = resources
                .Where(r => string.Equals(r.Type, "file", StringComparison.OrdinalIgnoreCase) &&
                            IsManagedBackupName(r.Name))
                .OrderByDescending(r => r.CreatedUtc ?? DateTime.MinValue)
                .ThenByDescending(r => r.Name, StringComparer.Ordinal)
                .ToList();

            foreach (var stale in managed.Skip(keepCount))
            {
                try
                {
                    await _apiClient.DeleteResourceAsync("app:/" + stale.Name, ct).ConfigureAwait(false);
                    _log.Information("Старая облачная копия удалена при ротации: {Name}", stale.Name);
                }
                catch (YandexDiskAuthException)
                {
                    // 401 не глотаем: единая обработка в CompleteSuccessAsync — инвалидация токена
                    // и честный AuthRequired без потери уже подтверждённого успеха загрузки.
                    throw;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.Warning(ex, "Не удалось удалить старую облачную копию {Name}", stale.Name);
                }
            }
        }

        private async Task<bool> TryVerifyLocalSnapshotAsync(string filePath, CancellationToken ct)
        {
            try
            {
                // Pooling=false: файл очереди сразу после проверки удаляется/пересоздаётся,
                // а закрытое в пул соединение удерживало бы хендл и мешало File.Delete.
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = filePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString();

                await using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync(ct).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA integrity_check";
                var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
                return string.Equals(result, "ok", StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Error(ex, "integrity_check не выполнен для снимка {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Создание снимка во временной папке очереди: только прошедший проверку целостности файл
        /// атомарно (переимованием на одном томе) попадает в очередь. При ошибке частичный
        /// снимок удаляется; отмена пробрасывается как OperationCanceledException.
        /// </summary>
        private async Task<string?> CreateVerifiedSnapshotAsync(CancellationToken ct)
        {
            var stagingDir = Path.Combine(_queueDirectory, StagingDirectoryName);
            try
            {
                Directory.CreateDirectory(stagingDir);
                CleanDirectory(stagingDir); // аварийное завершение прошлого запуска могло что-то оставить

                // Фабрика снимка без побочных эффектов локального бэкапа (раздел 5 аудита):
                // ротация осталась в CreateBackupAsync, ошибки приходят исключениями, отмена — OCE.
                var staged = await _settingsService.CreateSqliteSnapshotAsync(stagingDir, ct).ConfigureAwait(false);
                if (!File.Exists(staged))
                {
                    _log.Error("Сервис настроек вернул несуществующий файл снимка: {StagedPath}", staged);
                    return null;
                }
                if (!await TryVerifyLocalSnapshotAsync(staged, ct).ConfigureAwait(false))
                {
                    // Производный снимок, а не исходные данные — удаляем, исходная БД не затрагивается.
                    _log.Warning("Созданный снимок не прошёл integrity_check и не будет отправлен: {StagedPath}", staged);
                    return null;
                }

                var queueFile = Path.Combine(_queueDirectory, Path.GetFileName(staged));
                File.Move(staged, queueFile, overwrite: true);
                return queueFile;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Не удалось создать снимок для облачной выгрузки");
                return null;
            }
            finally
            {
                // Частичные снимки не должны оставаться ни в очереди, ни во временной папке.
                CleanDirectory(stagingDir);
            }
        }

        /// <summary>
        /// Повреждённый файл очереди — производный снимок, а не исходная БД: убираем его
        /// из активной маски, чтобы следующая попытка создала новый снимок (YD-04).
        /// </summary>
        private void QuarantineCorruptQueueFile(string queueFile)
        {
            try
            {
                // Суффикс .corrupt не попадает под маску AutoKassa_Backup_*.db.
                File.Move(queueFile, queueFile + ".corrupt", overwrite: true);
                _log.Warning("Повреждённый файл очереди отправлен в карантин: {QuarantinePath}", queueFile + ".corrupt");
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Не удалось отправить повреждённый файл в карантин — удаляем: {QueueFile}", queueFile);
                DeleteQueueFile(queueFile);
            }
        }

        private static void CleanDirectory(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return;
                foreach (var file in Directory.GetFiles(directory))
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Не удалось очистить временную папку снимков {Directory}", directory);
            }
        }

        private string? FindNewestQueueFile()
        {
            if (!Directory.Exists(_queueDirectory))
                return null;

            return Directory
                .GetFiles(_queueDirectory, BackupFileSearchPattern)
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private void PruneQueueToNewest()
        {
            if (!Directory.Exists(_queueDirectory))
                return;

            foreach (var extra in Directory
                .GetFiles(_queueDirectory, BackupFileSearchPattern)
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .Skip(1))
            {
                DeleteQueueFile(extra);
            }
        }

        private static void DeleteQueueFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Не удалось удалить файл очереди {FilePath}", filePath);
            }
        }

        private bool IsDue(int autoBackupDays)
        {
            if (_lastSuccessUtc == null)
                return true;

            var interval = _time.GetUtcNow().UtcDateTime - _lastSuccessUtc.Value;
            return interval >= TimeSpan.FromDays(autoBackupDays);
        }

        private string BuildUpToDateMessage()
            => _lastSuccessUtc == null
                ? "Облачная копия актуальна."
                // Локальное время без метки UTC: рядом в UI показывается CloudLastSuccessText
                // в локальном времени — разные зоны рядом читались как расхождение дат.
                : $"Облачная копия актуальна (последняя: {_lastSuccessUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}).";

        private CloudBackupState Fail(CloudBackupStatus status, string userMessage, Exception? ex)
        {
            if (ex != null)
                _log.Error(ex, "Облачное копирование не выполнено: {UserMessage}", userMessage);

            _lastFailureUtc = _time.GetUtcNow().UtcDateTime;
            return SetState(status, userMessage);
        }

        private CloudBackupState SetState(CloudBackupStatus status, string userMessage)
        {
            var pendingFile = BuildPendingFileInfo();
            _state = new CloudBackupState(
                status,
                _lastSuccessUtc,
                pendingFile != null,
                userMessage,
                pendingFile,
                _lastFailureUtc);
            StateChanged?.Invoke(_state);
            return _state;
        }

        /// <summary>Метаданные ожидающего файла очереди для отображения в UI; null, если очередь пуста.</summary>
        private PendingQueueFileInfo? BuildPendingFileInfo()
        {
            try
            {
                var file = FindNewestQueueFile();
                if (file == null)
                    return null;

                var info = new FileInfo(file);
                return new PendingQueueFileInfo(info.Name, info.CreationTimeUtc, info.Length);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Не удалось прочитать метаданные файла очереди облачной выгрузки");
                return null;
            }
        }

        private static bool IsManagedBackupName(string name)
        {
            // Точный формат имени, который создаёт SettingsService.CreateSqliteSnapshotAsync:
            // AutoKassa_Backup_yyyyMMdd_HHmmss.db. Широкая маска *.db не допускается —
            // чужой файл с похожим именем в пользовательской зоне app:/ не является нашей копией.
            const string suffix = ".db";
            if (!name.StartsWith(BackupFilePrefix, StringComparison.Ordinal) ||
                !name.EndsWith(suffix, StringComparison.Ordinal))
                return false;

            var stamp = name.Substring(BackupFilePrefix.Length, name.Length - BackupFilePrefix.Length - suffix.Length);
            return DateTime.TryParseExact(
                stamp,
                "yyyyMMdd_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _);
        }

        private static bool IsSameFile(DiskResourceInfo resource, long fileSize, string fileMd5)
            => resource.Size == fileSize &&
               string.Equals(resource.Md5, fileMd5, StringComparison.OrdinalIgnoreCase);

        private static async Task<string> ComputeMd5Async(string filePath, CancellationToken ct)
        {
            // MD5 здесь — контроль целостности копии против метаданных Диска, не защита от подделки.
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = await MD5.HashDataAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
