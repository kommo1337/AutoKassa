using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoKassa.Migrations
{
    /// <inheritdoc />
    public partial class Net10Migration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Transaction_CreatedAt",
                table: "Transactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Date_Type_IsDeleted",
                table: "Transactions",
                columns: new[] { "Date", "Type", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Type_IsDeleted_Date",
                table: "Transactions",
                columns: new[] { "Type", "IsDeleted", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transaction_CreatedAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_Date_Type_IsDeleted",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_Type_IsDeleted_Date",
                table: "Transactions");
        }
    }
}
