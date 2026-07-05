using AutoKassa.Models;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Запрос на загрузку списка операций.
    /// </summary>
    public record TransactionListRequest(
        TransactionFilterParameters Filters,
        int PageSize,
        int CurrentPage);
}
