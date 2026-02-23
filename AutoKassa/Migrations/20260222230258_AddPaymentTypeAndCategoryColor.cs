using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoKassa.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTypeAndCategoryColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Categories",
                type: "TEXT",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AutoBackupDays",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AutoGenerateReports",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoLockEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ConfirmDelete",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPageSize",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PasswordExpireDays",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequirePasswordOnStartup",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowNotifications",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOperationsInSidebar",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "WindowHeight",
                table: "AppSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "WindowWidth",
                table: "AppSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AutoBackupDays", "AutoGenerateReports", "AutoLockEnabled", "BackupEnabled", "ConfirmDelete", "DefaultPageSize", "DefaultPeriodFilter", "Language", "PasswordExpireDays", "RequirePasswordOnStartup", "ShowNotifications", "ShowOperationsInSidebar", "WindowHeight", "WindowWidth" },
                values: new object[] { 7, false, true, false, true, 20, "Month", "ru-RU", 0, true, true, false, 700.0, 1200.0 });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "Color",
                value: "#6366f1");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "Color",
                value: "#f59e0b");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "Color",
                value: "#14b8a6");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "Color",
                value: "#94a3b8");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 5,
                column: "Color",
                value: "#ec4899");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 6,
                column: "Color",
                value: "#f97316");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 7,
                column: "Color",
                value: "#8b5cf6");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 8,
                column: "Color",
                value: "#06b6d4");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 9,
                column: "Color",
                value: "#84cc16");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 10,
                column: "Color",
                value: "#ef4444");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_PaymentType",
                table: "Transactions",
                column: "PaymentType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transaction_PaymentType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "AutoBackupDays",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AutoGenerateReports",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AutoLockEnabled",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ConfirmDelete",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultPageSize",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "PasswordExpireDays",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "RequirePasswordOnStartup",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ShowNotifications",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ShowOperationsInSidebar",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "WindowHeight",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "WindowWidth",
                table: "AppSettings");

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BackupEnabled", "DefaultPeriodFilter" },
                values: new object[] { true, "CurrentMonth" });
        }
    }
}
