using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoKassa.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionsFts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Полнотекстовый индекс FTS5 по описанию операций (токенизатор unicode61
            // по умолчанию — регистронезависимый, в том числе для кириллицы).
            // Модель EF не меняется: таблица и триггеры живут только на уровне SQLite.
            migrationBuilder.Sql(
                @"CREATE VIRTUAL TABLE IF NOT EXISTS ""TransactionsFts"" USING fts5(""Description"");

CREATE TRIGGER IF NOT EXISTS ""TransactionsFts_Insert"" AFTER INSERT ON ""Transactions"" BEGIN
    INSERT INTO ""TransactionsFts""(rowid, ""Description"") VALUES (new.""Id"", new.""Description"");
END;

CREATE TRIGGER IF NOT EXISTS ""TransactionsFts_Update"" AFTER UPDATE OF ""Description"" ON ""Transactions"" BEGIN
    UPDATE ""TransactionsFts"" SET ""Description"" = new.""Description"" WHERE rowid = old.""Id"";
END;

CREATE TRIGGER IF NOT EXISTS ""TransactionsFts_Delete"" AFTER DELETE ON ""Transactions"" BEGIN
    DELETE FROM ""TransactionsFts"" WHERE rowid = old.""Id"";
END;

INSERT INTO ""TransactionsFts""(rowid, ""Description"")
    SELECT t.""Id"", t.""Description"" FROM ""Transactions"" t
    WHERE NOT EXISTS (SELECT 1 FROM ""TransactionsFts"" f WHERE f.rowid = t.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP TRIGGER IF EXISTS ""TransactionsFts_Insert"";
DROP TRIGGER IF EXISTS ""TransactionsFts_Update"";
DROP TRIGGER IF EXISTS ""TransactionsFts_Delete"";
DROP TABLE IF EXISTS ""TransactionsFts"";");
        }
    }
}
