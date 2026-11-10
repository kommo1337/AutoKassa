using AutoKassa.Services.Backup;

namespace AutoKassa.Tests.Services.Backup
{
    /// <summary>Фейк IAutomaticBackupService для тестов: подсчёт вызовов без фоновых потоков.</summary>
    public sealed class FakeAutomaticBackupService : IAutomaticBackupService
    {
        public int StartCallCount { get; private set; }
        public int RequestCheckCallCount { get; private set; }
        public int StopCallCount { get; private set; }

        public void Start() => StartCallCount++;

        public void RequestCheck() => RequestCheckCallCount++;

        public Task StopAsync(TimeSpan? timeout = null)
        {
            StopCallCount++;
            return Task.CompletedTask;
        }
    }
}
