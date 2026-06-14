using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoKassa.Migrations
{
    /// <inheritdoc />
    public partial class FixCreditCardPendingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditCardCurrentDebt",
                table: "AppSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditCardInterestRate",
                table: "AppSettings",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreditCardLastPaymentDate",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditCardLimit",
                table: "AppSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditCardMinimumPaymentPercent",
                table: "AppSettings",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CreditCardPaymentDay",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreditCardCurrentDebt", "CreditCardInterestRate", "CreditCardLastPaymentDate", "CreditCardLimit", "CreditCardMinimumPaymentPercent", "CreditCardPaymentDay" },
                values: new object[] { 0m, 0m, null, 0m, 5m, 10 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditCardCurrentDebt",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CreditCardInterestRate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CreditCardLastPaymentDate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CreditCardLimit",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CreditCardMinimumPaymentPercent",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CreditCardPaymentDay",
                table: "AppSettings");
        }
    }
}
