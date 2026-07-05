using AutoKassa.Models;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Сервис для валидации и сохранения операции, добавленной через inline-форму.
    /// </summary>
    public interface IInlineTransactionService
    {
        /// <summary>
        /// Пытается сохранить inline-операцию. Возвращает сохранённую операцию или null при ошибке валидации.
        /// </summary>
        /// <param name="input">Входные данные операции.</param>
        /// <param name="showError">Делегат для показа ошибки валидации.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Сохранённая операция или null.</returns>
        Task<Transaction?> TrySaveAsync(
            InlineTransactionInput input,
            Action<string> showError,
            CancellationToken ct = default);
    }
}
