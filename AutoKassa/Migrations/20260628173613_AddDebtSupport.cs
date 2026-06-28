using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoKassa.Migrations
{
    /// <inheritdoc />
    public partial class AddDebtSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Transactions",
                type: "TEXT",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<int>(
                name: "CounterpartyId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DebtStatus",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Counterparties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT COLLATE NOCASE", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counterparties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DebtPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DebtTransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RepaymentTransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebtPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DebtPayments_Transactions_DebtTransactionId",
                        column: x => x.DebtTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebtPayments_Transactions_RepaymentTransactionId",
                        column: x => x.RepaymentTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_CounterpartyId",
                table: "Transactions",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_DebtStatus",
                table: "Transactions",
                column: "DebtStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Counterparty_IsActive",
                table: "Counterparties",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Counterparty_Name",
                table: "Counterparties",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counterparty_Type",
                table: "Counterparties",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_DebtPayment_DebtTransactionId",
                table: "DebtPayments",
                column: "DebtTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_DebtPayment_DebtTransactionId_RepaymentTransactionId",
                table: "DebtPayments",
                columns: new[] { "DebtTransactionId", "RepaymentTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebtPayment_RepaymentTransactionId",
                table: "DebtPayments",
                column: "RepaymentTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Counterparties_CounterpartyId",
                table: "Transactions",
                column: "CounterpartyId",
                principalTable: "Counterparties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Counterparties_CounterpartyId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "Counterparties");

            migrationBuilder.DropTable(
                name: "DebtPayments");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_CounterpartyId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_DebtStatus",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CounterpartyId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DebtStatus",
                table: "Transactions");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Transactions",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
