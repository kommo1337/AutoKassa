using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoKassa.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditCardPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditCardPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepaymentTransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreditCardPurchaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditCardPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditCardPayments_CreditCardPurchases_CreditCardPurchaseId",
                        column: x => x.CreditCardPurchaseId,
                        principalTable: "CreditCardPurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditCardPayments_Transactions_RepaymentTransactionId",
                        column: x => x.RepaymentTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditCardPayment_CreditCardPurchaseId",
                table: "CreditCardPayments",
                column: "CreditCardPurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCardPayment_RepaymentTransactionId",
                table: "CreditCardPayments",
                column: "RepaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCardPayment_RepaymentTransactionId_CreditCardPurchaseId",
                table: "CreditCardPayments",
                columns: new[] { "RepaymentTransactionId", "CreditCardPurchaseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditCardPayments");
        }
    }
}
