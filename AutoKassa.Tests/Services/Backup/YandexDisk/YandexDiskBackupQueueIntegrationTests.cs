using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Services.Backup.YandexDisk;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Xunit;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// Интеграционный тест облачной очереди с настоящим SettingsService (раздел 6 аудита):
    /// снимок создаётся через SQLite Online Backup API реального сервиса настроек,
    /// дата успеха персистится в настоящей файловой БД, очередь переиспользует файл.
    /// </summary>
    public class YandexDiskBackupQueueIntegrationTests : IDisposable
    {
        private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly string _rootDir;
        private readonly string _dbPath;
        private readonly string _queueDir;
        private readonly TestDbContextFactory _factory;
        private readonly SettingsService _settings;
        private readonly FakeYandexDiskApiClient _api;
        private readonly FakeYandexDiskTokenStore _tokenStore;
        private readonly FakeTimeProvider _time;
        private readonly YandexDiskAuthService _auth;
        private readonly YandexDiskBackupService _svc;

        public YandexDiskBackupQueueIntegrationTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "autokassa-ydqueue-it", Guid.NewGuid().ToString("N"));
            _dbPath = Path.Combine(_rootDir, "AutoKassa.db");
            _queueDir = Path.Combine(_rootDir, "queue");
            Directory.CreateDirectory(_rootDir);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_dbPath}")
                .Options;
            _factory = new TestDbContextFactory(options);

            // Реальные миграции + WAL, как при старте приложения.
            SettingsService.MigrateAsync(_factory).GetAwaiter().GetResult();

            _settings = new SettingsService(_factory);
            _api = new FakeYandexDiskApiClient();
            _tokenStore = new FakeYandexDiskTokenStore();
            _time = new FakeTimeProvider(StartTime);
            _auth = new YandexDiskAuthService(
                new YandexDiskOptions { ClientId = "test-client" },
                _tokenStore,
                new HttpClient(new FakeHttpMessageHandler()),
                _time);
            _svc = new YandexDiskBackupService(_settings, _auth, _api, _time, _queueDir);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
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
            await _tokenStore.SaveAsync(new YandexDiskToken("tok", StartTime.UtcDateTime + TimeSpan.FromDays(30)));
        }

        /// <summary>GetResource возвращает метаданные, совпадающие с последним загруженным файлом.</summary>
        private void SetupMatchingResource()
        {
            _api.GetResourceAsyncFunc = (remotePath, _) =>
            {
                var local = _api.UploadedLocalPaths.LastOrDefault();
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

        [Fact]
        public async Task UploadNow_WithRealSettingsService_CreatesSnapshotPersistsDateAndClearsQueue()
        {
            await ConnectAsync();
            SetupMatchingResource();

            var result = await _svc.UploadNowAsync(force: true);

            result.Success.Should().BeTrue("прогон: " + result.State);
            Directory.Exists(_queueDir).Should().BeTrue();
            Directory.GetFiles(_queueDir, "AutoKassa_Backup_*.db").Should().BeEmpty("успешная выгрузка очищает очередь");
            _api.UploadFileCallCount.Should().Be(1);

            // Дата успеха записана настоящим SettingsService в настоящую БД.
            var persisted = await _settings.GetSettingsAsync();
            persisted.YandexDiskLastBackupUtc.Should().Be(StartTime.UtcDateTime);
        }

        [Fact]
        public async Task UploadNow_AuthFailure_KeepsQueueThenReusesSnapshotAfterReconnect()
        {
            await ConnectAsync();
            _api.GetUploadLinkAsyncFunc = (_, _, _) => throw new YandexDiskAuthException("HTTP 401");

            var failed = await _svc.UploadNowAsync(force: true);

            failed.Success.Should().BeFalse();
            failed.State.Status.Should().Be(CloudBackupStatus.AuthRequired);
            var pending = Directory.GetFiles(_queueDir, "AutoKassa_Backup_*.db").SingleOrDefault();
            pending.Should().NotBeNull("снимок создан реальным SettingsService и остался в очереди");

            // Повторный вход: новый экземпляр auth-сервиса над тем же хранилищем токена
            // (реальный вход через браузер сбрасывает состояние; в памяти старого
            // экземпляра остался бы AuthRequired). Очередь переиспользует тот же файл.
            await ConnectAsync();
            var reauthed = new YandexDiskAuthService(
                new YandexDiskOptions { ClientId = "test-client" },
                _tokenStore,
                new HttpClient(new FakeHttpMessageHandler()),
                _time);
            var restarted = new YandexDiskBackupService(_settings, reauthed, _api, _time, _queueDir);
            SetupMatchingResource();
            _api.GetUploadLinkAsyncFunc = (_, _, _) => Task.FromResult(new DiskUploadLink("https://upload.example.test/put", "PUT"));
            _api.ResetCounters();

            var retried = await restarted.UploadNowAsync(force: true);

            retried.Success.Should().BeTrue("повторный прогон: " + retried.State);
            _api.UploadFileCallCount.Should().Be(1, "содержимое очереди выгружается повторно");
            Directory.GetFiles(_queueDir, "AutoKassa_Backup_*.db").Should().BeEmpty();
            (await _settings.GetSettingsAsync()).YandexDiskLastBackupUtc.Should().NotBeNull();
        }

        private static string Md5Hex(byte[] bytes)
            => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }
}
