using AutoKassa.Models;
using AutoKassa.Models.Enums;
using System.Globalization;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Реализация сервиса валидации и сохранения inline-операций.
    /// </summary>
    public class InlineTransactionService : IInlineTransactionService
    {
        private readonly ITransactionService _transactionService;

        /// <summary>
        /// Создаёт экземпляр <see cref="InlineTransactionService"/>.
        /// </summary>
        /// <param name="transactionService">Сервис операций.</param>
        public InlineTransactionService(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <inheritdoc />
        public async Task<Transaction?> TrySaveAsync(
            InlineTransactionInput input,
            Action<string> showError,
            CancellationToken ct = default)
        {
            if (!decimal.TryParse(
                    input.AmountText?.Replace(',', '.'),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out decimal amount)
                || amount <= 0)
            {
                showError("Введите корректную сумму");
                return null;
            }

            if (input.Category == null)
            {
                showError("Выберите категорию");
                return null;
            }

            if (input.PaymentType == PaymentType.CreditCard && input.CreditCard == null)
            {
                showError("Выберите кредитную карту");
                return null;
            }

            // Соответствует [MaxLength(500)] в Models/Transaction.cs
            if (input.Description?.Length > 500)
            {
                showError("Описание не должно превышать 500 символов");
                return null;
            }

            var transaction = new Transaction
            {
                Date = input.Date,
                Amount = amount,
                Type = input.Type,
                CategoryId = input.Category.Id,
                Description = input.Description ?? string.Empty,
                PaymentType = input.PaymentType,
                CreditCardId = input.PaymentType == PaymentType.CreditCard ? input.CreditCard?.Id : null
            };

            await _transactionService.AddAsync(transaction);
            return transaction;
        }
    }
}
