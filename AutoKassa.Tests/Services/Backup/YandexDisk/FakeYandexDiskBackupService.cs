using AutoKassa.Models.Enums;
using AutoKassa.Services.Backup.YandexDisk;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// Фейк IYandexDiskBackupService для тестов: подсчёт вызовов, управляемый результат,
    /// ручной подъём StateChanged.
    /// </summary>
    public sealed class FakeYandexDiskBackupService : IYandexDiskBackupService
    {
        private readonly List<bool> _uploadForceArgs = new();

        public CloudBackupState CurrentState { get; set; } =
            new(CloudBackupStatus.NotConnected, null, false, "Яндекс Диск не подключён.");

        /// <summary>Выбрасывается из UploadNowAsync, если задано.</summary>
        public Exception? UploadException { get; set; }

        /// <summary>Если задано — вызывается вместо стандартного успешного результата.</summary>
        public Func<bool, CancellationToken, Task<CloudBackupResult>>? UploadOverride { get; set; }

        /// <summary>Если задано — вызывается вместо стандартного успешного результата DiscardPendingAndUploadAsync.</summary>
        public Func<CancellationToken, Task<CloudBackupResult>>? DiscardPendingOverride { get; set; }

        public int UploadCallCount { get; private set; }
        public int DiscardPendingCallCount { get; private set; }
        public IReadOnlyList<bool> UploadForceArgs => _uploadForceArgs;

        public event Action<CloudBackupState>? StateChanged;

        public Task<CloudBackupResult> UploadNowAsync(bool force = false, CancellationToken ct = default)
        {
            UploadCallCount++;
            _uploadForceArgs.Add(force);
            if (UploadException != null)
                throw UploadException;

            if (UploadOverride != null)
                return UploadOverride(force, ct);

            return Task.FromResult(new CloudBackupResult(true, CurrentState));
        }

        public Task<CloudBackupResult> DiscardPendingAndUploadAsync(CancellationToken ct = default)
        {
            DiscardPendingCallCount++;
            if (DiscardPendingOverride != null)
                return DiscardPendingOverride(ct);

            return Task.FromResult(new CloudBackupResult(true, CurrentState));
        }

        public void RaiseStateChanged(CloudBackupState state)
        {
            CurrentState = state;
            StateChanged?.Invoke(state);
        }
    }
}
