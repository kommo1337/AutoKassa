using AutoKassa.Models.Enums;
using AutoKassa.Services.Backup.YandexDisk;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using System.Security.Cryptography;
using Xunit;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    public class YandexDiskBackupServiceTests : IDisposable
    {
        private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly string _rootDir;
        private readonly string _queueDir;
        private readonly FakeSettingsService _settings;
        private readonly FakeYandexDiskApiClient _api;
        private readonly FakeYandexDiskTokenStore _tokenStore;
        private readonly FakeTimeProvider _time;
        private readonly YandexDiskAuthService _auth;
        private readonly YandexDiskBackupService _svc;

        public YandexDiskBackupServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "autokassa-ydbackupsvc-tests", Guid.NewGuid().ToString("N"));
            _queueDir = Path.Combine(_rootDir, "queue");
            _settings = new FakeSettingsService();
            _api = new FakeYandexDiskApiClient();
            _tokenStore = new FakeYandexDiskTokenStore();
            _time = new FakeTimeProvider(StartTime);

            var options = new YandexDiskOptions { ClientId = "test-client" };
            _auth = new YandexDiskAuthService(options, _tokenStore, new HttpClient(new FakeHttpMessageHandler()), _time);
            _svc = new YandexDiskBackupService(_settings, _auth, _api, _time, _queueDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_rootDir))
                    Directory.Delete(_rootDir, recursive: true);
            }
            catch
            {
                // Временный каталог не удалился — не ломаем тестовый прогон.
            }
        }

        private async Task ConnectAsync()
        {
            // Токен живёт 30 дней относительно фиксированных часов теста —
            // тесты могут продвигать время на дни, не ломая авторизацию.
            await _tokenStore.SaveAsync(new YandexDiskToken("tok", StartTime.UtcDateTime + TimeSpan.FromDays(30)));
        }

        /// <summary>GetResource возвращает метаданные, совпадающие с последним загруженным (или очередным) файлом.</summary>
        private void SetupMatchingResource()
        {
            _api.GetResourceAsyncFunc = (remotePath, _) =>
            {
                var local = _api.UploadedLocalPaths.LastOrDefault() ?? FindQueueFile();
                local.Should().NotBeNull("для сверки нужен локальный файл");
                var bytes = File.ReadAllBytes(local!);
                return Task.FromResult(new DiskResourceInfo(
                    Path.GetFileName(remotePath),
                    bytes.Length,
                    Md5Hex(bytes),
                    StartTime.UtcDateTime,
                    "file"));
            };
        }

        private string? FindQueueFile()
        {
            if (!Directory.Exists(_queueDir))
                return null;
            return Directory.GetFiles(_queueDir, "AutoKassa_Backup_*.db").OrderByDescending(f => f).FirstOrDefault();
        }

        [Fact]
        public async Task UploadNow_WithoutToken_SkipsSnapshotAndNetwork()
        {
            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            result.State.Status.Should().Be(CloudBackupStatus.NotConnected);
            _settings.CreateBackupCallCount.Should().Be(0);
            _api.GetUploadLinkCallCount.Should().Be(0);
            Directory.Exists(_queueDir).Should().BeFalse("очередь не должна создаваться без токена");
        }

        [Fact]
        public async Task UploadNow_WithExpiredToken_ReturnsAuthRequired()
        {
            await _tokenStore.SaveAsync(new YandexDiskToken("old-tok", StartTime.UtcDateTime - TimeSpan.FromHours(1)));

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.AuthRequired);
            _settings.CreateBackupCallCount.Should().Be(0);
            _api.GetUploadLinkCallCount.Should().Be(0);
        }

        [Fact]
        public async Task UploadNow_WhenDue_CreatesSnapshotUploadsAndClearsQueue()
        {
            await ConnectAsync();
            SetupMatchingResource();
            var statuses = new List<CloudBackupStatus>();
            _svc.StateChanged += state => statuses.Add(state.Status);

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            result.State.Status.Should().Be(CloudBackupStatus.UpToDate);
            result.State.LastSuccessUtc.Should().Be(StartTime.UtcDateTime);
            result.State.HasPendingFile.Should().BeFalse();

            _settings.CreateBackupCallCount.Should().Be(1);
            // Снимок создаётся во временной папке очереди и атомарно переходит в очередь после проверки (YD-04).
            _settings.CreateBackupPaths.Should().ContainSingle().Which.Should().Be(Path.Combine(_queueDir, ".staging"));

            _api.GetUploadLinkCallCount.Should().Be(1);
            _api.GetUploadLinkCalls.Should().ContainSingle()
                .Which.Should().Be(("app:/" + Path.GetFileName(_api.UploadedLocalPaths.Single()), false));

            _api.UploadFileCallCount.Should().Be(1);
            FindQueueFile().Should().BeNull("после подтверждённой выгрузки файл очереди удаляется");

            statuses.First().Should().Be(CloudBackupStatus.Uploading);
            statuses.Last().Should().Be(CloudBackupStatus.UpToDate);
        }

        [Fact]
        public async Task UploadNow_WhenNotDueAndNoForce_SkipsUpload()
        {
            await ConnectAsync();
            SetupMatchingResource();
            var firstRun = await _svc.UploadNowAsync(force: true);
            firstRun.Success.Should().BeTrue("первый прогон: " + firstRun.State);
            _api.ResetCounters();

            _time.Advance(TimeSpan.FromDays(1)); // копия вчерашняя, срок 7 дней — не подошёл
            var result = await _svc.UploadNowAsync(force: false);

            result.Success.Should().BeTrue("второй прогон: " + result.State);
            result.State.Status.Should().Be(CloudBackupStatus.UpToDate);
            _settings.CreateBackupCallCount.Should().Be(1, "второй прогон пропускается — снимок создавался только в первом");
            _api.UploadFileCallCount.Should().Be(0);
        }

        [Fact]
        public async Task UploadNow_Force_UploadsEvenWhenNotDue()
        {
            await ConnectAsync();
            SetupMatchingResource();
            await _svc.UploadNowAsync(force: true);
            _api.ResetCounters();

            _time.Advance(TimeSpan.FromDays(1));
            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue("второй прогон: " + result.State);
            _settings.CreateBackupCallCount.Should().Be(2, "снимок создаётся заново в каждом принудительном прогоне");
            _api.UploadFileCallCount.Should().Be(1);
        }

        [Fact]
        public async Task UploadNow_WithPendingFile_RetriesWithoutNewSnapshot()
        {
            await ConnectAsync();
            var attempts = 0;
            _api.UploadFileAsyncFunc = (_, _, _) =>
            {
                attempts++;
                throw new YandexDiskTransientException("сеть недоступна");
            };

            var first = await _svc.UploadNowAsync(force: true);

            first.Success.Should().BeFalse();
            first.State.Status.Should().Be(CloudBackupStatus.PendingRetry);
            first.State.HasPendingFile.Should().BeTrue();
            var pendingFile = FindQueueFile();
            pendingFile.Should().NotBeNull();
            _settings.CreateBackupCallCount.Should().Be(1);

            // Первая попытка исчерпала повторы. Настраиваем успех и запускаем вне срока —
            // файл из очереди переиспользуется независимо от срока.
            SetupMatchingResource();
            _api.UploadFileAsyncFunc = (_, _, _) => Task.FromResult(YandexDiskUploadResult.Created);
            _api.ResetCounters();
            _time.Advance(TimeSpan.FromMinutes(1));

            var second = await _svc.UploadNowAsync(force: false);

            second.Success.Should().BeTrue("второй прогон: " + second.State);
            second.State.Status.Should().Be(CloudBackupStatus.UpToDate);
            _settings.CreateBackupCallCount.Should().Be(1, "новый снимок не создаётся — переиспользуется очередь");
            _api.UploadFileCallCount.Should().Be(1);
            _api.UploadedLocalPaths.Should().ContainSingle().Which.Should().Be(pendingFile);
            FindQueueFile().Should().BeNull();
        }

        [Fact]
        public async Task UploadNow_TransientFailures_RetriesWithSameFile()
        {
            await ConnectAsync();
            SetupMatchingResource();
            var attempts = 0;
            _api.UploadFileAsyncFunc = (href, file, ct) =>
            {
                attempts++;
                if (attempts <= 2)
                    throw new YandexDiskTransientException("временный сбой");
                return Task.FromResult(YandexDiskUploadResult.Created);
            };

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            _api.UploadFileCallCount.Should().Be(3);
            _api.UploadedLocalPaths.Should().OnlyContain(p => p == _api.UploadedLocalPaths[0]);
            FindQueueFile().Should().BeNull();
        }

        [Fact]
        public async Task UploadNow_ConflictWithSameFile_TreatedAsSuccessWithoutReupload()
        {
            await ConnectAsync();
            _api.GetUploadLinkAsyncFunc = (_, _, _) => throw new YandexDiskConflictException("уже существует");
            _api.GetResourceAsyncFunc = (remotePath, _) =>
            {
                var local = FindQueueFile()!;
                var bytes = File.ReadAllBytes(local);
                return Task.FromResult(new DiskResourceInfo(
                    Path.GetFileName(remotePath), bytes.Length, Md5Hex(bytes), StartTime.UtcDateTime, "file"));
            };

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            result.State.Status.Should().Be(CloudBackupStatus.UpToDate);
            _api.UploadFileCallCount.Should().Be(0, "файл уже на Диске с тем же содержимым — повторная загрузка не нужна");
            FindQueueFile().Should().BeNull();
        }

        [Fact]
        public async Task UploadNow_ConflictWithDifferentFile_FailsAndKeepsQueue()
        {
            await ConnectAsync();
            _api.GetUploadLinkAsyncFunc = (_, _, _) => throw new YandexDiskConflictException("уже существует");
            _api.GetResourceAsyncFunc = (remotePath, _) =>
                Task.FromResult(new DiskResourceInfo(Path.GetFileName(remotePath), 999999, "deadbeef", StartTime.UtcDateTime, "file"));

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.Error);
            _api.UploadFileCallCount.Should().Be(0, "чужой файл перезаписывать запрещено");
            FindQueueFile().Should().NotBeNull("очередь сохраняется для разбора пользователем");
        }

        [Fact]
        public async Task UploadNow_AuthFailure_InvalidatesTokenAndKeepsQueue()
        {
            await ConnectAsync();
            _api.GetUploadLinkAsyncFunc = (_, _, _) => throw new YandexDiskAuthException("HTTP 401");

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.AuthRequired);
            _api.GetUploadLinkCallCount.Should().Be(1);
            (await _tokenStore.GetAsync()).Should().BeNull();
            (await _auth.GetStateAsync()).Should().Be(YandexDiskAuthState.AuthRequired);
            FindQueueFile().Should().NotBeNull("очередь не удаляется при 401");
        }

        [Fact]
        public async Task UploadNow_QuotaFailure_DoesNotRetry()
        {
            await ConnectAsync();
            _api.GetUploadLinkAsyncFunc = (_, _, _) => throw new YandexDiskQuotaException("нет места");

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.Error);
            _api.GetUploadLinkCallCount.Should().Be(1, "квотные ошибки не получают повторов");
            FindQueueFile().Should().NotBeNull();
        }

        [Fact]
        public async Task UploadNow_CorruptSnapshot_FailsWithoutNetworkAndSelfHealsOnNextRun()
        {
            await ConnectAsync();
            _settings.CreateBackupOverride = async dir =>
            {
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "AutoKassa_Backup_20260101_120000.db");
                await File.WriteAllTextAsync(path, "это не sqlite");
                return path;
            };

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.Error);
            _api.GetUploadLinkCallCount.Should().Be(0);
            _api.UploadFileCallCount.Should().Be(0);
            FindQueueFile().Should().BeNull("повреждённый снимок не попадает в очередь");
            Directory.GetFiles(_queueDir, "*", SearchOption.AllDirectories).Should().BeEmpty(
                "ни очередь, ни временная папка .staging не должны содержать частичный снимок");

            // Самовосстановление: следующая попытка создаёт новый снимок и доходит до выгрузки (YD-04).
            _settings.CreateBackupOverride = null;
            SetupMatchingResource();
            var second = await _svc.UploadNowAsync(force: true);

            second.Success.Should().BeTrue();
            _api.UploadFileCallCount.Should().Be(1);
            _settings.CreateBackupCallCount.Should().Be(2, "вторая попытка обязана создать новый снимок");
        }

        [Fact]
        public async Task UploadNow_CorruptPendingFile_QuarantinedAndRecreatedOnNextRun()
        {
            await ConnectAsync();
            Directory.CreateDirectory(_queueDir);
            var pending = Path.Combine(_queueDir, "AutoKassa_Backup_20260101_120000.db");
            await File.WriteAllTextAsync(pending, "это не sqlite");

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.Error);
            _api.UploadFileCallCount.Should().Be(0);
            FindQueueFile().Should().BeNull();
            File.Exists(pending + ".corrupt").Should().BeTrue("повреждённый файл очереди отправлен в карантин вне активной маски");

            // Карантин разблокировал очередь: новый снимок создаётся и выгружается.
            SetupMatchingResource();
            var second = await _svc.UploadNowAsync(force: true);

            second.Success.Should().BeTrue();
            _api.UploadFileCallCount.Should().Be(1);
        }

        [Fact]
        public async Task UploadNow_AcceptedUpload_PollsResourceUntilAvailable()
        {
            await ConnectAsync();
            _api.UploadFileAsyncFunc = (_, _, _) => Task.FromResult(YandexDiskUploadResult.Accepted);

            var resourceCalls = 0;
            _api.GetResourceAsyncFunc = (remotePath, _) =>
            {
                resourceCalls++;
                if (resourceCalls == 1)
                    throw new YandexDiskNotFoundException("ресурс ещё не появился");

                var local = _api.UploadedLocalPaths.Single();
                var bytes = File.ReadAllBytes(local);
                return Task.FromResult(new DiskResourceInfo(
                    Path.GetFileName(remotePath), bytes.Length, Md5Hex(bytes), StartTime.UtcDateTime, "file"));
            };

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            resourceCalls.Should().Be(2);
        }

        [Fact]
        public async Task UploadNow_CreatedUpload_WaitsUntilResourceAppears()
        {
            // YD-05 (долгосрочное): и после 201 ресурс может появиться с задержкой — ждём, а не падаем со сверкой.
            await ConnectAsync();
            var resourceCalls = 0;
            _api.GetResourceAsyncFunc = (remotePath, _) =>
            {
                resourceCalls++;
                if (resourceCalls <= 2)
                    throw new YandexDiskNotFoundException("ресурс ещё не появился");

                var local = _api.UploadedLocalPaths.Last();
                var bytes = File.ReadAllBytes(local);
                return Task.FromResult(new DiskResourceInfo(
                    Path.GetFileName(remotePath), bytes.Length, Md5Hex(bytes), StartTime.UtcDateTime, "file"));
            };

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            resourceCalls.Should().Be(3);
        }

        [Fact]
        public async Task UploadNow_MetadataAuthFailureAfterUpload_InvalidatesToken()
        {
            // YD-05: 401 при чтении метаданных после успешной загрузки — AuthRequired, а не «сверка не сошлась».
            await ConnectAsync();
            _api.GetResourceAsyncFunc = (_, _) => throw new YandexDiskAuthException("HTTP 401");

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.AuthRequired);
            (await _tokenStore.GetAsync()).Should().BeNull("токен инвалидируется при 401");
            FindQueueFile().Should().NotBeNull("очередь не удаляется при 401");
        }

        [Fact]
        public async Task UploadNow_MetadataTransientFailure_RetriesThenPendingRetry()
        {
            // YD-05: 5xx при чтении метаданных — временный сбой с повторами, а не терминальная ошибка сверки.
            await ConnectAsync();
            _api.GetResourceAsyncFunc = (_, _) => throw new YandexDiskTransientException("HTTP 500");

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.PendingRetry);
            _api.GetUploadLinkCallCount.Should().Be(3, "загрузка повторяется после временного сбоя метаданных");
            FindQueueFile().Should().NotBeNull();
        }

        [Fact]
        public async Task UploadNow_IdempotentConflictWithTransientMetadata_RetriesThenPendingRetry()
        {
            // YD-05: 5xx при сверке идемпотентного 409 — тоже повторяемый сбой.
            await ConnectAsync();
            _api.GetUploadLinkAsyncFunc = (_, _, _) => throw new YandexDiskConflictException("уже существует");
            _api.GetResourceAsyncFunc = (_, _) => throw new YandexDiskTransientException("HTTP 500 после 409");

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.PendingRetry);
            _api.GetUploadLinkCallCount.Should().Be(3);
            FindQueueFile().Should().NotBeNull();
        }

        [Fact]
        public async Task UploadNow_WhenApiTimesOut_RetriesThenPendingRetry()
        {
            // YD-07 (долгосрочное): медленный API длиннее собственного лимита запроса — таймаут
            // становится повторяемым сбоем, а не ложной «отменой» без ошибки.
            await ConnectAsync();
            var handler = new FakeHttpMessageHandler(async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"href\":\"https://upload.test/abc\",\"method\":\"PUT\"}")
                };
            });
            var options = new YandexDiskOptions
            {
                ClientId = "test",
                ApiBaseUrl = "https://cloud-api.test",
                ApiRequestTimeout = TimeSpan.FromMilliseconds(100)
            };
            var realClient = new YandexDiskApiClient(new HttpClient(handler), options, _auth);
            var svc = new YandexDiskBackupService(_settings, _auth, realClient, _time, _queueDir);

            var result = await svc.UploadNowAsync(force: true);

            result.Success.Should().BeFalse();
            result.State.Status.Should().Be(CloudBackupStatus.PendingRetry);
            handler.Requests.Should().HaveCount(3, "таймаут получает стандартные повторы");
            FindQueueFile().Should().NotBeNull("файл остаётся в очереди для следующей попытки");
        }

        [Fact]
        public async Task UploadNow_WhenDatePersistFails_KeepsQueueForIdempotentConfirm()
        {
            // Раздел 5 аудита: дата фиксируется до удаления pending-файла — при сбое записи даты
            // очередь остаётся, и следующий запуск подтверждает тот же облачный ресурс
            // идемпотентным 409 без повторной передачи данных.
            await ConnectAsync();
            SetupMatchingResource();
            _settings.SetYandexDiskLastBackupUtcException = new InvalidOperationException("БД настроек недоступна");

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue("выгрузка подтверждена — дата вторична");
            _settings.Settings.YandexDiskLastBackupUtc.Should().BeNull();
            FindQueueFile().Should().NotBeNull("pending сохранён для идемпотентного 409");

            // Второй запуск: ресурс уже на Диске — подтверждение по метаданным без повторной передачи.
            var uploadsAfterFirstRun = _api.UploadFileCallCount;
            _settings.SetYandexDiskLastBackupUtcException = null;
            _api.GetUploadLinkAsyncFunc = (_, _, _) => throw new YandexDiskConflictException("уже существует");
            var second = await _svc.UploadNowAsync(force: true);

            second.Success.Should().BeTrue();
            _api.UploadFileCallCount.Should().Be(uploadsAfterFirstRun, "повторная передача не нужна — файл уже на Диске");
            FindQueueFile().Should().BeNull("дата зафиксирована — файл очереди удалён");
            _settings.Settings.YandexDiskLastBackupUtc.Should().Be(StartTime.UtcDateTime);
        }

        [Fact]
        public async Task UploadNow_Rotation_DeletesOnlyOldestManagedFiles()
        {
            await ConnectAsync();
            SetupMatchingResource();
            _settings.Settings.BackupKeepCount = 2;
            var resources = new List<DiskResourceInfo>
            {
                new("AutoKassa_Backup_20250101_000000.db", 10, null, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), "file"),
                new("AutoKassa_Backup_20250301_000000.db", 10, null, new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), "file"),
                new("AutoKassa_Backup_20250501_000000.db", 10, null, new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc), "file"),
                new("Чужой файл.txt", 10, null, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), "file")
            };
            _api.GetAppFolderResourcesAsyncFunc = _ => Task.FromResult<IReadOnlyList<DiskResourceInfo>>(resources);

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            _api.DeleteResourceCallCount.Should().Be(1);
            _api.DeletedRemotePaths.Should().ContainSingle()
                .Which.Should().Be("app:/AutoKassa_Backup_20250101_000000.db");
        }

        [Fact]
        public async Task UploadNow_Rotation_NeverDeletesDirsOrSimilarNames()
        {
            await ConnectAsync();
            SetupMatchingResource();
            _settings.Settings.BackupKeepCount = 1;
            var resources = new List<DiskResourceInfo>
            {
                // Папка с управляемым именем — не резервная копия: удаление снесёт её содержимое безвозвратно.
                new("AutoKassa_Backup_20240501_000000.db", 0, null, new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc), "dir"),
                // Файл с «похожим» именем вне точного формата yyyyMMdd_HHmmss — чужой файл пользователя.
                new("AutoKassa_Backup_старая.db", 10, null, new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), "file"),
                new("AutoKassa_Backup_20240401_000000.db", 10, null, new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc), "file"),
                new("AutoKassa_Backup_20240601_000000.db", 10, null, new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), "file")
            };
            _api.GetAppFolderResourcesAsyncFunc = _ => Task.FromResult<IReadOnlyList<DiskResourceInfo>>(resources);

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            _api.DeletedRemotePaths.Should().BeEquivalentTo("app:/AutoKassa_Backup_20240401_000000.db");
            _api.DeletedRemotePaths.Should().NotContain(p => p.EndsWith("20240501_000000.db", StringComparison.Ordinal),
                "папка не удаляется даже с управляемым именем");
            _api.DeletedRemotePaths.Should().NotContain(p => p.Contains("старая", StringComparison.Ordinal),
                "файл вне точного формата имени не удаляется");
        }

        [Fact]
        public async Task UploadNow_RotationFailure_DoesNotFailConfirmedUpload()
        {
            await ConnectAsync();
            SetupMatchingResource();
            _api.DeleteResourceAsyncFunc = (_, _) => throw new Exception("удаление запрещено");

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            result.State.Status.Should().Be(CloudBackupStatus.UpToDate);
        }

        [Fact]
        public async Task UploadNow_Cancellation_LeavesQueueFile()
        {
            await ConnectAsync();
            _api.UploadFileAsyncFunc = async (_, _, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return YandexDiskUploadResult.Created;
            };
            using var cts = new CancellationTokenSource(200);

            var act = () => _svc.UploadNowAsync(force: true, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            FindQueueFile().Should().NotBeNull("при отмене файл остаётся в очереди");
            _svc.CurrentState.HasPendingFile.Should().BeTrue();
        }

        [Fact]
        public async Task UploadNow_Success_PersistsLastBackupDateToSettings()
        {
            await ConnectAsync();
            SetupMatchingResource();

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue();
            _settings.Settings.YandexDiskLastBackupUtc.Should().Be(StartTime.UtcDateTime,
                "дата подтверждённого успеха фиксируется в настройках (переживает перезапуск)");
        }

        [Fact]
        public async Task UploadNow_AfterRestart_UsesPersistedDateForDueCheck()
        {
            await ConnectAsync();
            SetupMatchingResource();
            var firstRun = await _svc.UploadNowAsync(force: true);
            firstRun.Success.Should().BeTrue("первый прогон: " + firstRun.State);
            _settings.Settings.YandexDiskLastBackupUtc.Should().NotBeNull();
            _api.ResetCounters();

            // «Перезапуск приложения»: новый экземпляр сервиса над теми же настройками и токеном.
            var restarted = new YandexDiskBackupService(_settings, _auth, _api, _time, _queueDir);
            _time.Advance(TimeSpan.FromDays(1)); // срок 7 дней — не подошёл

            var result = await restarted.UploadNowAsync(force: false);

            result.Success.Should().BeTrue("второй прогон: " + result.State);
            result.State.Status.Should().Be(CloudBackupStatus.UpToDate);
            _settings.CreateBackupCallCount.Should().Be(1, "снимок не создаётся — срок считается от сохранённой даты");
            _api.UploadFileCallCount.Should().Be(0);
        }

        [Fact]
        public async Task UploadNow_NoPriorSuccess_IsDueImmediately()
        {
            await ConnectAsync();
            _settings.Settings.YandexDiskLastBackupUtc = null;
            SetupMatchingResource();

            var result = await _svc.UploadNowAsync(force: false);

            result.Success.Should().BeTrue("первый прогон: " + result.State);
            _settings.CreateBackupCallCount.Should().Be(1, "без сохранённой даты копия считается просроченной");
        }

        private static string Md5Hex(byte[] bytes)
            => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }
}
