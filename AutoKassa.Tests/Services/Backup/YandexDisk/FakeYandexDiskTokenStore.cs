using AutoKassa.Services.Backup.YandexDisk;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// Временное in-memory хранилище токена Яндекс Диска для тестов.
    /// </summary>
    public sealed class FakeYandexDiskTokenStore : IYandexDiskTokenStore
    {
        private YandexDiskToken? _token;

        public Task<YandexDiskToken?> GetAsync(CancellationToken ct = default)
            => Task.FromResult(_token);

        public Task SaveAsync(YandexDiskToken token, CancellationToken ct = default)
        {
            _token = token;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken ct = default)
        {
            _token = null;
            return Task.CompletedTask;
        }
    }
}
