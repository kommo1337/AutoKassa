using AutoKassa.Models;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Результат загрузки страницы операций.
    /// </summary>
    public record TransactionListResult(
        IReadOnlyList<Transaction> Transactions,
        int TotalCount,
        int PageSize,
        int CurrentPage);
}
