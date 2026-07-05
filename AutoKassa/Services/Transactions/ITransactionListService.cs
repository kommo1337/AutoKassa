namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Сервис для загрузки страницы операций с учётом фильтров и пагинации.
    /// </summary>
    public interface ITransactionListService
    {
        /// <summary>
        /// Загружает первую страницу операций по заданным фильтрам.
        /// </summary>
        /// <param name="request">Запрос с фильтрами и параметрами пагинации.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Результат загрузки первой страницы.</returns>
        Task<TransactionListResult> LoadFirstPageAsync(
            TransactionListRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// Загружает следующую страницу операций, увеличивая номер страницы.
        /// </summary>
        /// <param name="request">Запрос с фильтрами и параметрами пагинации.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Результат загрузки следующей страницы.</returns>
        Task<TransactionListResult> LoadNextPageAsync(
            TransactionListRequest request,
            CancellationToken ct = default);
    }
}
