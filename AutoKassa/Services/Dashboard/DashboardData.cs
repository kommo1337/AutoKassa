using AutoKassa.Models;
using AutoKassa.ViewModels;
using OxyPlot;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Полный набор данных для отображения на дашборде.
    /// </summary>
    public record DashboardData(
        DashboardSummary Summary,
        DashboardPaymentBreakdown PaymentBreakdown,
        DashboardChange Changes,
        DashboardRecent Recent,
        DashboardToday Today,
        DashboardCash Cash,
        DashboardCredit Credit,
        PlotModel ChartModel);

    /// <summary>
    /// Сводка доходов/расходов/прибыли за период.
    /// </summary>
    public record DashboardSummary(
        decimal Income,
        decimal Expense,
        decimal Profit,
        decimal Profitability);

    /// <summary>
    /// Разбивка по типу оплаты.
    /// </summary>
    public record DashboardPaymentBreakdown(
        decimal CashIncome,
        decimal CashExpense,
        decimal CashProfit,
        decimal NonCashIncome,
        decimal NonCashExpense,
        decimal NonCashProfit);

    /// <summary>
    /// Изменение показателей относительно предыдущего периода (%).
    /// </summary>
    public record DashboardChange(
        decimal IncomeChangePercent,
        decimal ExpenseChangePercent,
        decimal ProfitChangePercent);

    /// <summary>
    /// Данные ленты последних операций, группировки, статистики и топа категорий.
    /// </summary>
    public record DashboardRecent(
        IReadOnlyList<Transaction> Transactions,
        IReadOnlyList<DateGroup> GroupedTransactions,
        int StatsCount,
        int StatsDays,
        decimal AvgExpense,
        decimal AvgIncome,
        IReadOnlyList<CategorySummaryItem> TopExpenses,
        IReadOnlyList<CategorySummaryItem> TopIncomes);

    /// <summary>
    /// Баланс за сегодня.
    /// </summary>
    public record DashboardToday(decimal Balance, bool HasTransactions);

    /// <summary>
    /// Фактический остаток в кассе.
    /// </summary>
    public record DashboardCash(
        decimal CashBalance,
        decimal NonCashBalance,
        decimal CurrentCashBalance);

    /// <summary>
    /// Метрики по кредитным картам.
    /// </summary>
    public record DashboardCredit(
        bool HasCreditCards,
        decimal TotalLimit,
        decimal TotalUsed,
        decimal TotalAvailable,
        decimal TotalDebt,
        DateTime? NextPaymentDate,
        decimal NextPaymentAmount);
}
