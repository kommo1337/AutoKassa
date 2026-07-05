using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoKassa.Migrations
{
    /// <inheritdoc />
    public partial class CreditCardPaymentCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditCardPayments_CreditCardPurchases_CreditCardPurchaseId",
                table: "CreditCardPayments");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditCardPayments_CreditCardPurchases_CreditCardPurchaseId",
                table: "CreditCardPayments",
                column: "CreditCardPurchaseId",
                principalTable: "CreditCardPurchases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditCardPayments_CreditCardPurchases_CreditCardPurchaseId",
                table: "CreditCardPayments");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditCardPayments_CreditCardPurchases_CreditCardPurchaseId",
                table: "CreditCardPayments",
                column: "CreditCardPurchaseId",
                principalTable: "CreditCardPurchases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
