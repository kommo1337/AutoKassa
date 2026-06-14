using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;
using Microsoft.EntityFrameworkCore;

namespace AutoKassa.Services
{
    /// <summary>
    /// Реализация сервиса отчетов
    /// </summary>
    public class ReportService : IReportService
    {
        /// <summary>
        /// Максимальное количество операций в отчёте «Детализация операций».
        /// Защита от RAM-всплеска и зависаний UI на старых ПК.
        /// </summary>
        private const int MaxDetailTransactions = 5000;

        /// <summary>
        /// Максимальное количество операций, загружаемых в память для отчёта «Структура по категориям».
        /// </summary>
        private const int MaxCategoryTransactions = 5000;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ISettingsService _settingsService;
        private readonly ICreditCardService _creditCardService;

        public ReportService(IDbContextFactory<AppDbContext> contextFactory, ISettingsService settingsService, ICreditCardService creditCardService)
        {
            _contextFactory = contextFactory;
            _settingsService = settingsService;
            _creditCardService = creditCardService;
        }

        /// <summary>
        /// Сформировать отчет "Баланс за период"
        /// </summary>
        public async Task<BalanceReport> GenerateBalanceReportAsync(DateTime dateFrom, DateTime dateTo, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var report = new BalanceReport
            {
                DateFrom = dateFrom,
                DateTo = dateTo
            };

            // Получаем начальный баланс
            report.StartBalance = await GetInitialBalanceAsync(dateFrom, paymentType, ct).ConfigureAwait(false);

            // Фильтр за период (включая весь последний день)
            var dateToEnd = dateTo.Date.AddDays(1);
            var query = context.Transactions
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Date >= dateFrom.Date && t.Date < dateToEnd);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);

            // SQL-агрегация итогов вместо загрузки всех транзакций в память
            var totals = await query
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            report.TotalIncome  = totals.FirstOrDefault(r => r.Type == OperationType.Income)?.Total  ?? 0m;
            report.TotalExpense = totals.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m;
            report.EndBalance   = report.StartBalance + report.TotalIncome - report.TotalExpense;

            // Кредитные покупки и обязательства
            report.TotalCreditPurchases = await GetCreditPurchasesAsync(context, dateFrom, dateToEnd, paymentType, ct).ConfigureAwait(false);
            report.TotalCreditDebt = await _creditCardService.GetTotalDebtAsync(ct).ConfigureAwait(false);
            report.FactBalance = await GetFactBalanceAsync(dateFrom, dateTo, paymentType, ct).ConfigureAwait(false);
            report.NetBalance = report.FactBalance - report.TotalCreditDebt;

            // SQL-агрегация по дням
            var dailyRows = await query
                .GroupBy(t => t.Date.Date)
                .Select(g => new
                {
                    Date    = g.Key,
                    Income  = (decimal)g.Sum(t => t.Type == OperationType.Income ? (double)t.Amount : 0),
                    Expense = (decimal)g.Sum(t => t.Type == OperationType.Expense ? (double)t.Amount : 0)
                })
                .OrderBy(x => x.Date)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var dailyDict = dailyRows.ToDictionary(r => r.Date, r => (r.Income, r.Expense));
            report.DailyBalances = new List<DailyBalance>();
            var currentBalance = report.StartBalance;

            for (var date = dateFrom.Date; date <= dateTo.Date; date = date.AddDays(1))
            {
                var (dayIncome, dayExpense) = dailyDict.TryGetValue(date, out var v) ? v : (0m, 0m);
                currentBalance += dayIncome - dayExpense;
                report.DailyBalances.Add(new DailyBalance
                {
                    Date    = date,
                    Income  = dayIncome,
                    Expense = dayExpense,
                    Balance = currentBalance
                });
            }

            return report;
        }

        /// <summary>
        /// Получить начальный баланс на дату
        /// </summary>
        public async Task<decimal> GetInitialBalanceAsync(DateTime date, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            // SQLite не поддерживает Sum для decimal — агрегируем через double на стороне БД,
            // чтобы не материализовать все транзакции в память.
            var query = context.Transactions
                .Where(t => !t.IsDeleted && t.Date < date.Date);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);

            var rows = await query
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var incomeBeforeDate  = rows.FirstOrDefault(r => r.Type == OperationType.Income)?.Total  ?? 0m;
            var expenseBeforeDate = rows.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m;

            var balance = incomeBeforeDate - expenseBeforeDate;

            // Начальный баланс из настроек относится только к наличным (касса)
            if (!paymentType.HasValue || paymentType.Value == PaymentType.Cash)
            {
                var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);
                balance += settings.InitialBalance;
            }

            return balance;
        }

        /// <summary>
        /// Сформировать отчет "Структура по категориям"
        /// </summary>
        public async Task<CategoryReport> GenerateCategoryReportAsync(DateTime dateFrom, DateTime dateTo, OperationType operationType, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var report = new CategoryReport
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                OperationType = operationType
            };

            // Фильтр за период с указанным типом (включая весь последний день)
            var dateToEnd = dateTo.Date.AddDays(1);
            var query = context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Where(t => !t.IsDeleted && t.Date >= dateFrom.Date && t.Date < dateToEnd && t.Type == operationType);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);

            // SQL-агрегация общей суммы и количества
            var totals = await query
                .GroupBy(_ => 1)
                .Select(g => new { TotalAmount = (decimal)g.Sum(t => (double)t.Amount), Count = g.Count() })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            report.TotalAmount = totals?.TotalAmount ?? 0m;
            report.TransactionCount = totals?.Count ?? 0;

            // Группировка по категориям — в памяти, т.к. UI требует List<Transaction> в каждой категории.
            // AsNoTracking + Include выше уже минимизируют накладные расходы EF.
            // Группируем по CategoryId (а не по ссылке на entity), т.к. при AsNoTracking EF создаёт
            // отдельные экземпляры Category для каждой строки и GroupBy по ссылке дал бы неверный результат.
            var transactions = await query.Take(MaxCategoryTransactions).ToListAsync(ct).ConfigureAwait(false);
            var categoryGroups = transactions
                .GroupBy(t => t.CategoryId)
                .Select(g => new
                {
                    Category = g.First().Category,
                    Amount = g.Sum(t => t.Amount),
                    Count = g.Count(),
                    Transactions = g.OrderByDescending(t => t.Date).ToList()
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            // Цвета для диаграммы
            var colors = new[]
            {
                "#2196F3", "#4CAF50", "#FF9800", "#E91E63", "#9C27B0",
                "#00BCD4", "#FFEB3B", "#795548", "#607D8B", "#F44336",
                "#3F51B5", "#009688", "#FFC107", "#673AB7", "#8BC34A"
            };

            // Формируем данные по категориям
            int colorIndex = 0;
            foreach (var group in categoryGroups)
            {
                var percentage = report.TotalAmount > 0
                    ? (double)(group.Amount / report.TotalAmount) * 100
                    : 0;

                report.CategoryItems.Add(new CategoryReportItem
                {
                    CategoryId = group.Category?.Id ?? 0,
                    CategoryName = group.Category?.Name ?? "Без категории",
                    Amount = group.Amount,
                    Percentage = Math.Round(percentage, 1),
                    TransactionCount = group.Count,
                    Color = colors[colorIndex % colors.Length],
                    Transactions = group.Transactions
                });

                colorIndex++;
            }

            return report;
        }

        /// <summary>
        /// Сформировать отчет "Детализация операций"
        /// </summary>
        public async Task<TransactionDetailReport> GenerateTransactionDetailReportAsync(
            DateTime dateFrom,
            DateTime dateTo,
            OperationType? operationType = null,
            int? categoryId = null,
            PaymentType? paymentType = null,
            CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var report = new TransactionDetailReport
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                FilterType = operationType,
                FilterCategoryId = categoryId
            };

            // Фильтр за период (включая весь последний день)
            var dateToEnd = dateTo.Date.AddDays(1);
            var query = context.Transactions
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Date >= dateFrom.Date && t.Date < dateToEnd);

            // Фильтр по типу оплаты
            if (paymentType.HasValue)
            {
                query = query.Where(t => t.PaymentType == paymentType.Value);
            }

            // Фильтр по типу операции
            if (operationType.HasValue)
            {
                query = query.Where(t => t.Type == operationType.Value);
            }

            // Фильтр по категории
            if (categoryId.HasValue)
            {
                query = query.Where(t => t.CategoryId == categoryId.Value);
            }

            // Проецируем сразу в DTO на стороне SQL — без материализации Transaction entities
            report.Transactions = await query
                .OrderBy(t => t.Date).ThenBy(t => t.CreatedAt)
                .Select(t => new TransactionDetailItem
                {
                    Id = t.Id,
                    Date = t.Date,
                    Type = t.Type,
                    Amount = t.Amount,
                    CategoryName = t.Category.Name ?? "Без категории",
                    Description = t.Description ?? string.Empty,
                    CreatedAt = t.CreatedAt
                })
                .Take(MaxDetailTransactions)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // Получаем название категории для фильтра
            if (categoryId.HasValue)
            {
                var category = await context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == categoryId.Value, ct)
                    .ConfigureAwait(false);
                report.FilterCategoryName = category?.Name;
            }

            // SQL-агрегация итогов
            var totals = await query
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            report.TotalIncome  = totals.FirstOrDefault(r => r.Type == OperationType.Income)?.Total  ?? 0m;
            report.TotalExpense = totals.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m;

            return report;
        }

        /// <summary>
        /// Получить сумму кредитных покупок за период
        /// </summary>
        private async Task<decimal> GetCreditPurchasesAsync(
            AppDbContext context,
            DateTime dateFrom,
            DateTime dateToEnd,
            PaymentType? paymentType,
            CancellationToken ct)
        {
            if (paymentType.HasValue && paymentType.Value != PaymentType.CreditCard)
                return 0m;

            var query = context.Transactions
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Date >= dateFrom.Date && t.Date < dateToEnd && t.Type == OperationType.Expense);

            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            else
                query = query.Where(t => t.PaymentType == PaymentType.CreditCard);

            return (decimal?)await query.SumAsync(t => (double)t.Amount, ct).ConfigureAwait(false) ?? 0m;
        }

        /// <summary>
        /// Получить фактический баланс (наличные + безналичные) за период
        /// </summary>
        private async Task<decimal> GetFactBalanceAsync(
            DateTime dateFrom,
            DateTime dateTo,
            PaymentType? paymentType,
            CancellationToken ct)
        {
            if (paymentType == PaymentType.CreditCard)
                return 0m;

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dateToEnd = dateTo.Date.AddDays(1);

            // Фактический начальный баланс: наличные (с начальным балансом из настроек) + безналичные
            var cashStart = await GetInitialBalanceAsync(dateFrom, PaymentType.Cash, ct).ConfigureAwait(false);
            var nonCashStart = await GetInitialBalanceAsync(dateFrom, PaymentType.NonCash, ct).ConfigureAwait(false);
            var startBalance = cashStart + nonCashStart;

            // Доходы и расходы по фактическим деньгам за период
            var query = context.Transactions
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Date >= dateFrom.Date && t.Date < dateToEnd);

            if (paymentType.HasValue)
            {
                query = query.Where(t => t.PaymentType == paymentType.Value);
            }
            else
            {
                query = query.Where(t => t.PaymentType == PaymentType.Cash || t.PaymentType == PaymentType.NonCash);
            }

            var totals = await query
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var income = totals.FirstOrDefault(r => r.Type == OperationType.Income)?.Total ?? 0m;
            var expense = totals.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m;

            return startBalance + income - expense;
        }
    }
}
