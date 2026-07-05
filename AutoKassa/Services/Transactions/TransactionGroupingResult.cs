using AutoKassa.ViewModels;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Результат группировки операций по датам.
    /// </summary>
    public record TransactionGroupingResult(
        IReadOnlyList<SelectableDateGroup> Groups,
        decimal TotalIncome,
        decimal TotalExpense);
}
