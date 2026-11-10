using AutoKassa.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// Проверяет реальные миграции (не EnsureCreated) на временном файле БД:
    /// существующие и новые установки получают выключенную облачную функцию.
    /// </summary>
    public class YandexDiskBackupMigrationTests : IDisposable
    {
        private readonly string _dbPath;

        public YandexDiskBackupMigrationTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"autokassa-migration-{Guid.NewGuid():N}.db");
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(_dbPath))
                    File.Delete(_dbPath);
            }
            catch
            {
                // Временный файл не удалился — не ломаем тестовый прогон.
            }
        }

        private DbContextOptions<AppDbContext> CreateOptions() =>
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_dbPath}")
                .Options;

        [Fact]
        public async Task Migrate_OnFreshDatabase_SeedsCloudBackupDefaultsDisabled()
        {
            await using var ctx = new AppDbContext(CreateOptions());
            await ctx.Database.MigrateAsync();

            var settings = await ctx.AppSettings.SingleAsync();
            settings.YandexDiskBackupEnabled.Should().BeFalse("облачный бэкап по умолчанию выключен");
            settings.YandexDiskLastBackupUtc.Should().BeNull("даты успешной выгрузки ещё не было");
        }

        [Fact]
        public async Task Migrate_FromExistingSettingsRow_AddsColumnsWithDisabledDefaults()
        {
            // Полная схема текущих миграций...
            await using (var ctx = new AppDbContext(CreateOptions()))
            {
                await ctx.Database.MigrateAsync();
            }

            // ...откатываемся к состоянию «до облачной миграции»: убираем новые столбцы
            // и запись о её применении, помечаем локальный бэкап включённым.
            SqliteConnection.ClearAllPools();
            await using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = @"
                    ALTER TABLE AppSettings DROP COLUMN YandexDiskBackupEnabled;
                    ALTER TABLE AppSettings DROP COLUMN YandexDiskLastBackupUtc;
                    UPDATE AppSettings SET BackupEnabled = 1 WHERE Id = 1;
                    DELETE FROM __EFMigrationsHistory WHERE MigrationId = '20261108164216_AddYandexDiskBackupSettings';";
                await command.ExecuteNonQueryAsync();
            }
            SqliteConnection.ClearAllPools();

            // Повторная миграция применит AddYandexDiskBackupSettings к существующей строке.
            await using var migrated = new AppDbContext(CreateOptions());
            await migrated.Database.MigrateAsync();

            var settings = await migrated.AppSettings.SingleAsync();
            settings.YandexDiskBackupEnabled.Should().BeFalse("существующая строка получает выключенный облачный бэкап");
            settings.YandexDiskLastBackupUtc.Should().BeNull();
            settings.BackupEnabled.Should().BeTrue("существующие значения не трогаем");
        }
    }
}
