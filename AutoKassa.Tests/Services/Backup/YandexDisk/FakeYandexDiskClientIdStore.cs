using AutoKassa.Services.Backup.YandexDisk;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>Фейк IYandexDiskClientIdStore: хранит значение в памяти.</summary>
    public sealed class FakeYandexDiskClientIdStore : IYandexDiskClientIdStore
    {
        public string? Stored { get; set; }
        public int SaveCallCount { get; private set; }
        public string? LastSaved { get; private set; }

        /// <summary>Если задано — выбрасывается из SaveAsync.</summary>
        public Exception? SaveException { get; set; }

        public Task<string?> GetAsync(CancellationToken ct = default)
            => Task.FromResult(Stored);

        public Task SaveAsync(string clientId, CancellationToken ct = default)
        {
            SaveCallCount++;
            LastSaved = clientId;
            if (SaveException != null)
                throw SaveException;

            Stored = clientId;
            return Task.CompletedTask;
        }
    }
}
