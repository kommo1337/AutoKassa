using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoKassa.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultOperationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultExpenseCategoryId",
                table: "AppSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultIncomeCategoryId",
                table: "AppSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultOperationType",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DefaultExpenseCategoryId", "DefaultIncomeCategoryId", "DefaultOperationType" },
                values: new object[] { null, null, 2 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultExpenseCategoryId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultIncomeCategoryId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultOperationType",
                table: "AppSettings");
        }
    }
}
