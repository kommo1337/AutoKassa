using AutoKassa.Models;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Реализация сервиса загрузки страниц операций с учётом фильтров и пагинации.
    /// </summary>
    public class TransactionListService : ITransactionListService
    {
        private readonly ITransactionService _transactionService;

        /// <summary>
        /// Создаёт экземпляр <see cref="TransactionListService"/>.
        /// </summary>
        /// <param name="transactionService">Сервис операций.</param>
        public TransactionListService(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <inheritdoc />
        public async Task<TransactionListResult> LoadFirstPageAsync(
            TransactionListRequest request,
            CancellationToken ct = default)
        {
            var filters = CopyFilters(request.Filters);
            filters.Skip = 0;
            filters.Take = request.PageSize;

            var transactions = await _transactionService.GetTransactionsAsync(filters, ct);
            var totalCount = await _transactionService.GetTotalCountAsync(filters, ct);

            return new TransactionListResult(
                transactions,
                totalCount,
                request.PageSize,
                0);
        }

        /// <inheritdoc />
        public async Task<TransactionListResult> LoadNextPageAsync(
            TransactionListRequest request,
            CancellationToken ct = default)
        {
            var newPage = request.CurrentPage + 1;
            var filters = CopyFilters(request.Filters);
            filters.Skip = newPage * request.PageSize;
            filters.Take = request.PageSize;

            var transactions = await _transactionService.GetTransactionsAsync(filters, ct);

            return new TransactionListResult(
                transactions,
                0, // total count не меняется при подгрузке
                request.PageSize,
                newPage);
        }

        /// <summary>
        /// Создаёт копию параметров фильтрации, чтобы не мутировать исходный запрос.
        /// </summary>
        /// <param name="source">Исходные параметры фильтрации.</param>
        /// <returns>Новый экземпляр <see cref="TransactionFilterParameters"/> с теми же значениями.</returns>
        private static TransactionFilterParameters CopyFilters(TransactionFilterParameters source)
        {
            return new TransactionFilterParameters
            {
                DateFrom = source.DateFrom,
                DateTo = source.DateTo,
                Type = source.Type,
                PaymentType = source.PaymentType,
                CategoryId = source.CategoryId,
                CounterpartyId = source.CounterpartyId,
                IsDebtFilter = source.IsDebtFilter,
                DebtStatus = source.DebtStatus,
                SearchText = source.SearchText,
                AmountFrom = source.AmountFrom,
                AmountTo = source.AmountTo,
                Skip = source.Skip,
                Take = source.Take,
                SortBy = source.SortBy,
                SortDescending = source.SortDescending
            };
        }
    }
}
