using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using Microsoft.Data.Sqlite;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// Минимальная реализация ISettingsService для тестов облачного бэкапа.
    /// CreateBackupAsync создаёт настоящий файл SQLite (валидный для PRAGMA integrity_check).
    /// </summary>
    public sealed class FakeSettingsService : ISettingsService
    {
        private readonly AppSettings _settings = new() { Id = 1, AutoBackupDays = 7, BackupKeepCount = 10 };

        /// <summary>Если задано — вызывается вместо стандартного создания снимка (например, для эмуляции сбоя или повреждённого файла).</summary>
        public Func<string, Task<string?>>? CreateBackupOverride { get; set; }

        /// <summary>Если задано — выбрасывается из SetYandexDiskLastBackupUtcAsync (эмуляция сбоя БД настроек).</summary>
        public Exception? SetYandexDiskLastBackupUtcException { get; set; }

        /// <summary>Если задано — вызывается вместо стандартного no-op при проверке локального бэкапа.</summary>
        public Func<CancellationToken, Task>? RunAutoBackupIfDueOverride { get; set; }

        /// <summary>Сколько раз вызывали CreateBackupAsync/CreateSqliteSnapshotAsync.</summary>
        public int CreateBackupCallCount { get; private set; }

        /// <summary>Пути, переданные в CreateBackupAsync/CreateSqliteSnapshotAsync.</summary>
        public List<string> CreateBackupPaths { get; } = new();

        /// <summary>Сколько раз вызывали RunAutoBackupIfDueAsync.</summary>
        public int RunAutoBackupIfDueCallCount { get; private set; }

        public AppSettings Settings => _settings;

        public Task<string?> CreateBackupAsync(string backupPath, CancellationToken ct = default)
        {
            // Старый контракт локального бэкапа: ошибки и отмена → null.
            try
            {
                return CreateSqliteSnapshotAsync(backupPath, ct)!;
            }
            catch
            {
                return Task.FromResult<string?>(null);
            }
        }

        public async Task<string> CreateSqliteSnapshotAsync(string targetDirectory, CancellationToken ct = default)
        {
            CreateBackupCallCount++;
            CreateBackupPaths.Add(targetDirectory);

            if (CreateBackupOverride != null)
            {
                var overridden = await CreateBackupOverride(targetDirectory);
                // Override с null — тестовый сбой создания снимка.
                return overridden ?? throw new InvalidOperationException("тестовый сбой создания снимка");
            }

            Directory.CreateDirectory(targetDirectory);
            var filePath = Path.Combine(targetDirectory, $"AutoKassa_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            if (File.Exists(filePath))
                File.Delete(filePath);

            using var connection = new SqliteConnection($"Data Source={filePath};Pooling=false");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE TestData (Id INTEGER PRIMARY KEY, Value TEXT); INSERT INTO TestData VALUES (1, 'ok');";
            command.ExecuteNonQuery();

            return filePath;
        }

        public Task<AppSettings> GetSettingsAsync() => Task.FromResult(_settings);

        /// <summary>Фиксирует дату успеха в настройках фейка (зеркало реального SetYandexDiskLastBackupUtcAsync).</summary>
        public Task SetYandexDiskLastBackupUtcAsync(DateTime? value, CancellationToken ct = default)
        {
            if (SetYandexDiskLastBackupUtcException != null)
                throw SetYandexDiskLastBackupUtcException;

            _settings.YandexDiskLastBackupUtc = value;
            return Task.CompletedTask;
        }

        #region Не используются в тестах облачного бэкапа

        public AppSettings GetSettings() => _settings;
        public bool IsPasswordSet() => true;
        public int GetAutoLockTimeout() => _settings.AutoLockTimeout;
        public string GetTheme() => _settings.Theme!;
        public OperationType GetDefaultOperationType() => OperationType.Expense;
        public Task<int?> GetDefaultCategoryIdAsync(OperationType type) => Task.FromResult<int?>(null);
        public Task SetPasswordAsync(string passwordHash, SecurityQuestion? questionId, string answerHash, string customQuestion = null!) => Task.CompletedTask;
        public Task UpdatePasswordAsync(string newPasswordHash) => Task.CompletedTask;
        public Task SetAutoLockTimeoutAsync(int minutes) => Task.CompletedTask;
        public Task SetThemeAsync(string theme) => Task.CompletedTask;
        public Task SetDefaultOperationTypeAsync(OperationType type) => Task.CompletedTask;
        public Task SetDefaultCategoryIdAsync(OperationType type, int? categoryId) => Task.CompletedTask;
        public Task SaveSettingsAsync(AppSettings settings) => Task.CompletedTask;
        public Task ResetToDefaultsAsync() => Task.CompletedTask;
        public Task<bool> ExportSettingsAsync(string filePath) => Task.FromResult(true);
        public Task<bool> ImportSettingsAsync(string filePath) => Task.FromResult(true);
        public Task<bool> RestoreBackupAsync(string backupFilePath, CancellationToken ct = default) => Task.FromResult(true);

        public Task RunAutoBackupIfDueAsync(CancellationToken ct = default)
        {
            RunAutoBackupIfDueCallCount++;
            if (RunAutoBackupIfDueOverride != null)
                return RunAutoBackupIfDueOverride(ct);
            return Task.CompletedTask;
        }

        public Task<DateTime?> GetLastBackupDateAsync() => Task.FromResult<DateTime?>(null);
        public Task<string?> GetDatabasePathAsync() => Task.FromResult<string?>(null);

        #endregion
    }
}
