using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoKassa.Migrations
{
    /// <inheritdoc />
    public partial class FixIncomeCreditCardPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // До исправления бага операции ДОХОДА на кредитную карту (пополнение карты)
            // ошибочно создавали запись CreditCardPurchase, из-за чего долг по карте рос
            // вместо уменьшения. Удаляем такие записи: после исправления пополнение
            // уменьшает долг через саму операцию дохода (см. CreditCardOperationHelper.GetCurrentDebtAsync).
            // OperationType.Income = 1. Связанные CreditCardPayment удаляются каскадом БД.
            migrationBuilder.Sql(
                @"DELETE FROM ""CreditCardPurchases""
                  WHERE ""TransactionId"" IN (SELECT ""Id"" FROM ""Transactions"" WHERE ""Type"" = 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
