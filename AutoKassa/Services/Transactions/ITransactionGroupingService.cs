using AutoKassa.Models;
using AutoKassa.ViewModels;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Сервис для группировки операций по датам и расчёта итогов.
    /// </summary>
    public interface ITransactionGroupingService
    {
        /// <summary>
        /// Группирует операции по дням, рассчитывает доходы/расходы и инициализирует inline-состояние групп.
        /// </summary>
        /// <param name="transactions">Список операций для группировки.</param>
        /// <param name="categories">Доступные категории для inline-форм.</param>
        /// <param name="creditCards">Доступные кредитные карты для inline-форм.</param>
        /// <param name="onGroupSave">Делегат сохранения групповой inline-операции.</param>
        /// <param name="selectionChanged">Делегат изменения выделения элемента.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Результат группировки с итогами.</returns>
        Task<TransactionGroupingResult> BuildGroupsAsync(
            IEnumerable<Transaction> transactions,
            IEnumerable<Category> categories,
            IEnumerable<CreditCard> creditCards,
            Func<SelectableDateGroup, Task> onGroupSave,
            Action selectionChanged,
            CancellationToken ct = default);
    }
}
