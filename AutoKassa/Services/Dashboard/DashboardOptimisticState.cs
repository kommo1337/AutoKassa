using AutoKassa.Models;
using AutoKassa.ViewModels;
using System;
using System.Collections.Generic;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Состояние дашборда, на основе которого выполняется оптимистичное обновление.
    /// </summary>
    public record DashboardOptimisticState(
        IReadOnlyList<Transaction> RecentTransactions,
        IReadOnlyList<DateGroup> GroupedTransactions,
        decimal TotalIncome,
        decimal TotalExpense,
        DateTime DateFrom,
        DateTime DateTo);
}
