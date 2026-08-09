using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.ViewModels;
using System.Collections.ObjectModel;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Реализация фасадного сервиса дашборда.
    /// Инкапсулирует SQL-агрегации и расчёт метрик.
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly ITransactionService _transactionService;
        private readonly ISettingsService _settingsService;
        private readonly ICreditCardService _creditCardService;

        private static readonly DateTime Epoch = new(2000, 1, 1);

        public DashboardService(
            ITransactionService transactionService,
            ISettingsService settingsService,
            ICreditCardService creditCardService)
        {
            _transactionService = transactionService;
            _settingsService = settingsService;
            _creditCardService = creditCardService;
        }

        /// <inheritdoc />
        public async Task<DashboardData> LoadDashboardDataAsync(
            DateTime dateFrom,
            DateTime dateTo,
            CancellationToken ct = default)
        {
            var summaryTask = GetSummaryAsync(dateFrom, dateTo, ct);
            // Лента переиспользует итоги периода из summaryTask, не дублируя запрос
            var recentTask = GetRecentDataAsync(dateFrom, dateTo, summaryTask, ct);
            var todayTask = GetTodayBalanceAsync(ct);
            var cashTask = GetCashBalanceAsync(ct);
            var creditTask = GetCreditMetricsAsync(ct);

            await Task.WhenAll(summaryTask, recentTask, todayTask, cashTask, creditTask);

            var summary = await summaryTask;

            return new DashboardData(
                summary.Summary,
                summary.PaymentBreakdown,
                summary.Changes,
                await recentTask,
                await todayTask,
                await cashTask,
                await creditTask);
        }

        private async Task<(DashboardSummary Summary, DashboardPaymentBreakdown PaymentBreakdown, DashboardChange Changes, int IncomeCount, int ExpenseCount)>
            GetSummaryAsync(DateTime dateFrom, DateTime dateTo, CancellationToken ct)
        {
            var allTask = _transactionService.GetPeriodTotalsAsync(dateFrom, dateTo, ct: ct);
            var cashTask = _transactionService.GetPeriodTotalsAsync(dateFrom, dateTo, PaymentType.Cash, ct);
            var cardTask = _transactionService.GetPeriodTotalsAsync(dateFrom, dateTo, PaymentType.NonCash, ct);
            await Task.WhenAll(allTask, cashTask, cardTask);

            var (income, expense, incCount, expCount) = await allTask;
            var profit = income - expense;
            var profitability = income > 0 ? ((income - expense) / income) * 100 : 0;

            var (ci, ce, _, _) = await cashTask;
            var cashProfit = ci - ce;
            var (ni, ne, _, _) = await cardTask;
            var nonCashProfit = ni - ne;

            var changes = await CalculateChangesAsync(dateFrom, dateTo, income, expense, profit, ct);

            return (
                new DashboardSummary(income, expense, profit, profitability),
                new DashboardPaymentBreakdown(ci, ce, cashProfit, ni, ne, nonCashProfit),
                changes,
                incCount,
                expCount);
        }

        private async Task<DashboardChange> CalculateChangesAsync(
            DateTime dateFrom,
            DateTime dateTo,
            decimal totalIncome,
            decimal totalExpense,
            decimal profit,
            CancellationToken ct)
        {
            var periodLength = (dateTo - dateFrom).Days + 1;
            var prevDateTo = dateFrom.AddDays(-1);
            var prevDateFrom = prevDateTo.AddDays(-periodLength + 1);

            var (prevIncome, prevExpense, _, _) =
                await _transactionService.GetPeriodTotalsAsync(prevDateFrom, prevDateTo, ct: ct);
            var prevProfit = prevIncome - prevExpense;

            var incomeChange = prevIncome > 0
                ? ((totalIncome - prevIncome) / prevIncome) * 100
                : (totalIncome > 0 ? 100 : 0);

            var expenseChange = prevExpense > 0
                ? ((totalExpense - prevExpense) / prevExpense) * 100
                : (totalExpense > 0 ? 100 : 0);

            var profitChange = prevProfit != 0
                ? ((profit - prevProfit) / Math.Abs(prevProfit)) * 100
                : (profit != 0 ? 100 : 0);

            return new DashboardChange(incomeChange, expenseChange, profitChange);
        }

        private async Task<DashboardRecent> GetRecentDataAsync(
            DateTime dateFrom,
            DateTime dateTo,
            Task<(DashboardSummary Summary, DashboardPaymentBreakdown PaymentBreakdown, DashboardChange Changes, int IncomeCount, int ExpenseCount)> summaryTask,
            CancellationToken ct)
        {
            var filters = new TransactionFilterParameters
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                Take = 100
            };
            var list = await _transactionService.GetTransactionsAsync(filters, ct);

            var grouped = await Task.Run(() =>
            {
                return list
                    .GroupBy(t => t.Date.Date)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new DateGroup
                    {
                        Date = g.Key,
                        DayTotal = g.Sum(t => t.Type == OperationType.Income ? t.Amount : -t.Amount),
                        Items = new ObservableCollection<Transaction>(g.OrderByDescending(t => t.CreatedAt))
                    })
                    .ToList();
            }, ct);

            var dailyTask = _transactionService.GetDailyTotalsAsync(dateFrom, dateTo, ct: ct);
            var topExpTask = _transactionService.GetTopCategoriesAsync(dateFrom, dateTo, OperationType.Expense, 5, ct: ct);
            var topIncTask = _transactionService.GetTopCategoriesAsync(dateFrom, dateTo, OperationType.Income, 5, ct: ct);

            await Task.WhenAll(dailyTask, topExpTask, topIncTask);

            // Количества операций за период берём из уже посчитанной сводки
            var (_, _, _, incCount, expCount) = await summaryTask;
            var dailyGroups = await dailyTask;

            var statsCount = await _transactionService.GetTotalCountAsync(
                new TransactionFilterParameters { DateFrom = dateFrom, DateTo = dateTo, Take = 1 }, ct);
            var statsDays = dailyGroups.Count;
            var totalIncome = list.Where(t => t.Type == OperationType.Income).Sum(t => t.Amount);
            var totalExpense = list.Where(t => t.Type == OperationType.Expense).Sum(t => t.Amount);
            var avgExpense = expCount > 0 ? totalExpense / expCount : 0;
            var avgIncome = incCount > 0 ? totalIncome / incCount : 0;

            var topExpenses = (await topExpTask)
                .Select(x => new CategorySummaryItem { Name = x.Name, Total = x.Total })
                .ToList();
            var topIncomes = (await topIncTask)
                .Select(x => new CategorySummaryItem { Name = x.Name, Total = x.Total })
                .ToList();

            return new DashboardRecent(
                list,
                grouped,
                statsCount,
                statsDays,
                avgExpense,
                avgIncome,
                topExpenses,
                topIncomes);
        }

        private async Task<DashboardToday> GetTodayBalanceAsync(CancellationToken ct)
        {
            var cashTask = _transactionService.GetPeriodTotalsAsync(DateTime.Today, DateTime.Today, PaymentType.Cash, ct);
            var cardTask = _transactionService.GetPeriodTotalsAsync(DateTime.Today, DateTime.Today, PaymentType.NonCash, ct);
            await Task.WhenAll(cashTask, cardTask);

            var (cashInc, cashExp, cashIncCount, cashExpCount) = await cashTask;
            var (cardInc, cardExp, cardIncCount, cardExpCount) = await cardTask;

            var hasTransactions = (cashIncCount + cashExpCount + cardIncCount + cardExpCount) > 0;
            var balance = (cashInc - cashExp) + (cardInc - cardExp);

            return new DashboardToday(balance, hasTransactions);
        }

        private async Task<DashboardCash> GetCashBalanceAsync(CancellationToken ct)
        {
            var settings = await _settingsService.GetSettingsAsync();

            // Один запрос с группировкой по типу оплаты вместо двух полных сканов истории
            var totals = await _transactionService.GetPeriodTotalsByPaymentTypeAsync(Epoch, DateTime.Today, ct);

            var cash = totals.FirstOrDefault(t => t.PaymentType == PaymentType.Cash);
            var cashBalance = settings.InitialBalance + cash.Income - cash.Expense;

            var nonCash = totals.FirstOrDefault(t => t.PaymentType == PaymentType.NonCash);
            var nonCashBalance = nonCash.Income - nonCash.Expense;

            return new DashboardCash(cashBalance, nonCashBalance, cashBalance + nonCashBalance);
        }

        private async Task<DashboardCredit> GetCreditMetricsAsync(CancellationToken ct)
        {
            // Метрики по всем картам одним пакетом, без N+1
            var metrics = await _creditCardService.GetCardsMetricsAsync(ct);
            var activeMetrics = metrics.Where(m => m.Card.IsActive).ToList();

            if (activeMetrics.Count == 0)
            {
                return new DashboardCredit(false, 0, 0, 0, 0, null, 0);
            }

            var totalLimit = activeMetrics.Sum(m => m.Card.Limit);
            var totalAvailable = activeMetrics.Sum(m => m.AvailableLimit);
            var totalUsed = totalLimit - totalAvailable;
            var totalDebt = activeMetrics.Sum(m => m.CurrentDebt);

            var upcoming = activeMetrics
                .Where(m => m.NextPaymentDate.HasValue && m.CurrentDebt > 0)
                .OrderBy(m => m.NextPaymentDate)
                .FirstOrDefault();

            DateTime? nextDate = null;
            decimal nextAmount = 0;
            if (upcoming != null)
            {
                nextDate = upcoming.NextPaymentDate!.Value;
                nextAmount = activeMetrics
                    .Where(m => m.NextPaymentDate == upcoming.NextPaymentDate)
                    .Sum(m => m.MinimumPayment);
            }

            return new DashboardCredit(true, totalLimit, totalUsed, totalAvailable, totalDebt, nextDate, nextAmount);
        }
    }
}
