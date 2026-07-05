using AutoKassa.Models;
using AutoKassa.Models.Enums;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Входные данные для inline-добавления операции.
    /// </summary>
    public record InlineTransactionInput(
        string AmountText,
        OperationType Type,
        Category Category,
        PaymentType PaymentType,
        CreditCard? CreditCard,
        string Description,
        DateTime Date);
}
