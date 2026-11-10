using AutoKassa.Services.Backup.YandexDisk;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// DPAPI привязан к текущему Windows-пользователю: round-trip выполняется на машине
    /// разработчика и в CI под той учётной записью, которая запускает тесты.
    /// </summary>
    public class DpapiYandexDiskTokenStoreTests : IDisposable
    {
        private readonly string _directory;
        private readonly string _filePath;

        public DpapiYandexDiskTokenStoreTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "autokassa-dpapi-tests", Guid.NewGuid().ToString("N"));
            _filePath = Path.Combine(_directory, "yandex-disk-token.bin");
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

        private DpapiYandexDiskTokenStore CreateStore() => new(_filePath);

        [Fact]
        public async Task GetAsync_WhenFileMissing_ReturnsNull()
        {
            var store = CreateStore();

            (await store.GetAsync()).Should().BeNull();
        }

        [Fact]
        public async Task SaveAndGet_RoundTripsToken()
        {
            var store = CreateStore();
            var token = new YandexDiskToken("access-token-abc", new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc));

            await store.SaveAsync(token);

            File.Exists(_filePath).Should().BeTrue();
            var loaded = await store.GetAsync();
            loaded.Should().NotBeNull();
            loaded!.AccessToken.Should().Be(token.AccessToken);
            loaded.ExpiresAtUtc.Should().Be(token.ExpiresAtUtc);
        }

        [Fact]
        public async Task GetAsync_WhenFileCorrupted_ReturnsNullWithoutThrowing()
        {
            var store = CreateStore();
            Directory.CreateDirectory(_directory);
            await File.WriteAllBytesAsync(_filePath, new byte[] { 1, 2, 3, 4, 5 }); // не DPAPI-шифротекст

            var loaded = await store.GetAsync();

            loaded.Should().BeNull();
        }

        [Fact]
        public async Task ClearAsync_RemovesToken()
        {
            var store = CreateStore();
            await store.SaveAsync(new YandexDiskToken("tok", DateTime.UtcNow.AddHours(1)));

            await store.ClearAsync();

            (await store.GetAsync()).Should().BeNull();
            File.Exists(_filePath).Should().BeFalse();
        }

        [Fact]
        public async Task SaveAsync_ReplacesExistingToken()
        {
            var store = CreateStore();
            await store.SaveAsync(new YandexDiskToken("old-token", DateTime.UtcNow.AddHours(1)));
            var replacement = new YandexDiskToken("new-token", DateTime.UtcNow.AddHours(2));

            await store.SaveAsync(replacement);

            var loaded = await store.GetAsync();
            loaded!.AccessToken.Should().Be("new-token");
        }

        [Fact]
        public async Task SaveAsync_WhenReplaceFails_KeepsPreviousTokenReadable()
        {
            // YD-08 (долгосрочное): сбой между записью временного файла и заменой
            // не должен лишать приложение сохранённого токена.
            var store = CreateStore();
            await store.SaveAsync(new YandexDiskToken("previous-token", DateTime.UtcNow.AddHours(1)));

            var failing = new DpapiYandexDiskTokenStore(_filePath, (_, _) => throw new IOException("сбой замены"));
            var act = () => failing.SaveAsync(new YandexDiskToken("new-token", DateTime.UtcNow.AddHours(2)));

            await act.Should().ThrowAsync<IOException>();

            var loaded = await store.GetAsync();
            loaded.Should().NotBeNull("прежний токен уцелел при сбое замены");
            loaded!.AccessToken.Should().Be("previous-token");
        }

        [Fact]
        public async Task SaveAsync_LeavesNoTempFilesBehind()
        {
            // Уникальные имена временных файлов не должны накапливаться в каталоге.
            var store = CreateStore();
            for (var i = 0; i < 3; i++)
                await store.SaveAsync(new YandexDiskToken($"tok-{i}", DateTime.UtcNow.AddHours(1)));

            Directory.GetFiles(_directory, "*.tmp").Should().BeEmpty();
            Directory.GetFiles(_directory, "*.*.tmp").Should().BeEmpty();
        }
    }
}
