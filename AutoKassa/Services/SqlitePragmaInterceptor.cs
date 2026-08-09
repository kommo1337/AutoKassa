using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AutoKassa.Services
{
    /// <summary>
    /// Применяет per-connection PRAGMA SQLite при каждом открытии соединения.
    /// synchronous=NORMAL безопасен в WAL и заметно дешевле FULL на слабом железе;
    /// temp_store=MEMORY и увеличенный page-кэш (~8 МБ) ускоряют агрегации и сортировки.
    /// Эти настройки живут в рамках соединения и не сохраняются в файле БД,
    /// поэтому выставляются на каждом открытии, а не один раз при миграции
    /// (journal_mode=WAL, напротив, персистентна и применяется в SettingsService.MigrateAsync).
    /// </summary>
    public class SqlitePragmaInterceptor : DbConnectionInterceptor
    {
        private const string PragmaSql = "PRAGMA synchronous=NORMAL; PRAGMA temp_store=MEMORY; PRAGMA cache_size=-8000;";

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var command = connection.CreateCommand();
            command.CommandText = PragmaSql;
            command.ExecuteNonQuery();

            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = PragmaSql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
        }
    }
}
