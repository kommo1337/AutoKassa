using Microsoft.Data.Sqlite;

// Путь к БД
var dbPath = args.Length > 0 ? args[0] :
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
        "AutoKassa", "bin", "Debug", "net8.0-windows", "AutoKassa.db");

if (!File.Exists(dbPath))
{
    Console.WriteLine($"DB not found: {dbPath}");
    return 1;
}

Console.WriteLine($"Using DB: {dbPath}");

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

// 1. Создать таблицу истории если нет
ExecuteNonQuery(conn, @"
CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
    ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
    ""ProductVersion"" TEXT NOT NULL
);");

// 2. Вставить записи для предыдущих миграций (если их нет)
InsertMigrationIfNotExists(conn, "20260107002354_InitialCreate", "8.0.11");
InsertMigrationIfNotExists(conn, "20260107102336_AddDefaultOperationSettings", "8.0.11");

// 3. Применить новую миграцию (если ещё не применена)
var exists = (long)new SqliteCommand(
    "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '20260222230258_AddPaymentTypeAndCategoryColor'",
    conn).ExecuteScalar()! > 0;

if (exists)
{
    Console.WriteLine("Migration already applied.");
    return 0;
}

Console.WriteLine("Applying AddPaymentTypeAndCategoryColor migration...");

// Добавляем колонку PaymentType (default 1 = Cash)
TryExecute(conn, "ALTER TABLE Transactions ADD COLUMN PaymentType INTEGER NOT NULL DEFAULT 1;");

// Добавляем колонку Color
TryExecute(conn, "ALTER TABLE Categories ADD COLUMN Color TEXT NOT NULL DEFAULT '';");

// Обновляем цвета категорий
var colors = new Dictionary<int, string>
{
    [1] = "#6366f1",
    [2] = "#f59e0b",
    [3] = "#14b8a6",
    [4] = "#94a3b8",
    [5] = "#ec4899",
    [6] = "#f97316",
    [7] = "#8b5cf6",
    [8] = "#06b6d4",
    [9] = "#84cc16",
    [10] = "#ef4444",
};

foreach (var (id, color) in colors)
{
    ExecuteNonQuery(conn, $"UPDATE Categories SET Color = '{color}' WHERE Id = {id};");
}

// Создаём индекс
TryExecute(conn, "CREATE INDEX IF NOT EXISTS IX_Transaction_PaymentType ON Transactions(PaymentType);");

// Записываем в историю миграций
InsertMigrationIfNotExists(conn, "20260222230258_AddPaymentTypeAndCategoryColor", "8.0.11");

Console.WriteLine("Migration applied successfully!");
return 0;

void ExecuteNonQuery(SqliteConnection c, string sql)
{
    using var cmd = new SqliteCommand(sql, c);
    cmd.ExecuteNonQuery();
}

void TryExecute(SqliteConnection c, string sql)
{
    try { ExecuteNonQuery(c, sql); }
    catch (Exception ex) { Console.WriteLine($"  (skipped: {ex.Message})"); }
}

void InsertMigrationIfNotExists(SqliteConnection c, string migrationId, string version)
{
    var count = (long)new SqliteCommand(
        $"SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '{migrationId}'", c).ExecuteScalar()!;
    if (count == 0)
    {
        ExecuteNonQuery(c, $"INSERT INTO __EFMigrationsHistory VALUES ('{migrationId}', '{version}');");
        Console.WriteLine($"  Inserted migration history: {migrationId}");
    }
    else
    {
        Console.WriteLine($"  Already exists: {migrationId}");
    }
}
