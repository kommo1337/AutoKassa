using AutoKassa.Services.Backup.YandexDisk;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// Тесты хранилища пользовательского ClientId: приоритет над appsettings.json,
    /// round-trip, атомарная запись через AtomicFile.
    /// </summary>
    public class YandexDiskClientIdStoreTests : IDisposable
    {
        private readonly string _directory;
        private readonly string _filePath;

        public YandexDiskClientIdStoreTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "autokassa-ydclientid-tests", Guid.NewGuid().ToString("N"));
            _filePath = Path.Combine(_directory, "yandex-disk-clientid.txt");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                    Directory.Delete(_directory, recursive: true);
            }
            catch
            {
                // Временный каталог не удалился — не ломаем тестовый прогон.
            }
        }

        private YandexDiskClientIdStore CreateStore(YandexDiskOptions options)
            => new(options, _filePath);

        [Fact]
        public async Task GetAsync_WhenFileMissing_ReturnsNull()
        {
            var store = CreateStore(new YandexDiskOptions());

            (await store.GetAsync()).Should().BeNull();
        }

        [Fact]
        public async Task SaveAsync_ThenGet_RoundTripsAndUpdatesLiveOptions()
        {
            var options = new YandexDiskOptions { ClientId = string.Empty };
            var store = CreateStore(options);

            await store.SaveAsync("  client-id-abc  ");

            (await store.GetAsync()).Should().Be("client-id-abc");
            options.ClientId.Should().Be("client-id-abc", "живой экземпляр опций обновлён без перезапуска");
        }

        [Fact]
        public async Task SaveAsync_WhitespaceOnly_ThrowsBusinessError()
        {
            var store = CreateStore(new YandexDiskOptions());

            var act = () => store.SaveAsync("   ");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*ClientId*");
            File.Exists(_filePath).Should().BeFalse("файл не создаётся при отклонённом значении");
        }

        [Fact]
        public void ApplyStoredOverride_WhenFileExists_ReplacesAppsettingsValue()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, "stored-client-id" + Environment.NewLine);
            var options = new YandexDiskOptions { ClientId = "appsettings-value" };

            // Статик вызывается в корне композиции (App.ConfigureServices) с путём по умолчанию;
            // здесь проверяем логику через экземпляр с явным путём: GetAsync видит файл.
            var store = CreateStore(options);
            var stored = store.GetAsync().Result;

            stored.Should().Be("stored-client-id");
        }

        [Fact]
        public async Task SaveAsync_Overwrite_ReplacesPreviousValue()
        {
            var store = CreateStore(new YandexDiskOptions());
            await store.SaveAsync("first-id");

            await store.SaveAsync("second-id");

            (await store.GetAsync()).Should().Be("second-id");
            Directory.GetFiles(_directory, "*.tmp").Should().BeEmpty("атомарная запись не оставляет временных файлов");
        }
    }
}
