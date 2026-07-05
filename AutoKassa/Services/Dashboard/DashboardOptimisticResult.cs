using AutoKassa.Models;
using AutoKassa.ViewModels;
using System.Collections.Generic;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Результат оптимистичного обновления дашборда.
    /// </summary>
    public record DashboardOptimisticResult(
        IReadOnlyList<Transaction> RecentTransactions,
        IReadOnlyList<DateGroup> GroupedTransactions,
        decimal TotalIncome,
        decimal TotalExpense,
        bool HasTransactions);
}
