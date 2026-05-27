using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly AppDbContext _context;
        private readonly ISettingsService _settingsService;

        public ReportService(AppDbContext context, ISettingsService settingsService)
        {
            _context = context;
            _settingsService = settingsService;
        }

        /// <summary>
        /// Сформировать отчет "Баланс за период"
        /// </summary>
        public async Task<BalanceReport> GenerateBalanceReportAsync(DateTime dateFrom, DateTime dateTo, PaymentType? paymentType = null)
        {
            var report = new BalanceReport
            {
                DateFrom = dateFrom,
                DateTo = dateTo
            };

            // Получаем начальный баланс
            report.StartBalance = await GetInitialBalanceAsync(dateFrom, paymentType);

            // Получаем операции за период (включая весь последний день)
            var dateToEnd = dateTo.Date.AddDays(1);
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => !t.IsDeleted && t.Date >= dateFrom.Date && t.Date < dateToEnd);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var transactions = await query
                .OrderBy(t => t.Date)
                .ToListAsync();

            // Считаем доходы и расходы
            report.TotalIncome = transactions
                .Where(t => t.Type == OperationType.Income)
                .Sum(t => t.Amount);

            report.TotalExpense = transactions
                .Where(t => t.Type == OperationType.Expense)
                .Sum(t => t.Amount);

            report.EndBalance = report.StartBalance + report.TotalIncome - report.TotalExpense;

            // Формируем данные по дням
            report.DailyBalances = GenerateDailyBalances(
                dateFrom,
                dateTo,
                report.StartBalance,
                transactions
            );

            return report;
        }

        /// <summary>
        /// Получить начальный баланс на дату
        /// </summary>
        public async Task<decimal> GetInitialBalanceAsync(DateTime date, PaymentType? paymentType = null)
        {
            // SQLite не поддерживает Sum для decimal — агрегируем через double на стороне БД,
            // чтобы не материализовать все транзакции в память.
            var query = _context.Transactions
                .Where(t => !t.IsDeleted && t.Date < date.Date);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);

            var rows = await query
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount) })
                .ToListAsync();

            var incomeBeforeDate  = rows.FirstOrDefault(r => r.Type == OperationType.Income)?.Total  ?? 0m;
            var expenseBeforeDate = rows.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m;

            var balance = incomeBeforeDate - expenseBeforeDate;

            // Начальный баланс из настроек относится только к наличным (касса)
            if (!paymentType.HasValue || paymentType.Value == PaymentType.Cash)
            {
                var settings = await _settingsService.GetSettingsAsync();
                balance += settings.InitialBalance;
            }

            return balance;
        }

        /// <summary>
        /// Сформировать данные баланса по дням
        /// </summary>
        private List<DailyBalance> GenerateDailyBalances(
            DateTime dateFrom,
            DateTime dateTo,
            decimal startBalance,
            List<Models.Transaction> transactions)
        {
            var dailyBalances = new List<DailyBalance>();
            var currentBalance = startBalance;

            // Группируем операции по дням
            var transactionsByDay = transactions
                .GroupBy(t => t.Date.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Проходим по всем дням периода
            for (var date = dateFrom.Date; date <= dateTo.Date; date = date.AddDays(1))
            {
                var dayIncome = 0m;
                var dayExpense = 0m;

                if (transactionsByDay.ContainsKey(date))
                {
                    var dayTransactions = transactionsByDay[date];
                    dayIncome = dayTransactions
                        .Where(t => t.Type == OperationType.Income)
                        .Sum(t => t.Amount);
                    dayExpense = dayTransactions
                        .Where(t => t.Type == OperationType.Expense)
                        .Sum(t => t.Amount);
                }

                currentBalance += dayIncome - dayExpense;

                dailyBalances.Add(new DailyBalance
                {
                    Date = date,
                    Income = dayIncome,
                    Expense = dayExpense,
                    Balance = currentBalance
                });
            }

            return dailyBalances;
        }

        /// <summary>
        /// Сформировать отчет "Структура по категориям"
        /// </summary>
        public async Task<CategoryReport> GenerateCategoryReportAsync(DateTime dateFrom, DateTime dateTo, OperationType operationType, PaymentType? paymentType = null)
        {
            var report = new CategoryReport
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                OperationType = operationType
            };

            // Получаем операции за период с указанным типом (включая весь последний день)
            var dateToEnd = dateTo.Date.AddDays(1);
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => !t.IsDeleted && t.Date >= dateFrom.Date && t.Date < dateToEnd && t.Type == operationType);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var transactions = await query.ToListAsync();

            // Общая сумма и количество операций
            report.TotalAmount = transactions.Sum(t => t.Amount);
            report.TransactionCount = transactions.Count;

            // Группируем по категориям
            var categoryGroups = transactions
                .GroupBy(t => t.Category)
                .Select(g => new
                {
                    Category = g.Key,
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
            PaymentType? paymentType = null)
        {
            var report = new TransactionDetailReport
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                FilterType = operationType,
                FilterCategoryId = categoryId
            };

            // Получаем операции за период (включая весь последний день)
            var dateToEnd = dateTo.Date.AddDays(1);
            var query = _context.Transactions
                .Include(t => t.Category)
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

            var transactions = await query.OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ToListAsync();

            // Получаем название категории для фильтра
            if (categoryId.HasValue)
            {
                var category = await _context.Categories.FindAsync(categoryId.Value);
                report.FilterCategoryName = category?.Name;
            }

            // Преобразуем в элементы отчета
            foreach (var transaction in transactions)
            {
                report.Transactions.Add(new TransactionDetailItem
                {
                    Id = transaction.Id,
                    Date = transaction.Date,
                    Type = transaction.Type,
                    Amount = transaction.Amount,
                    CategoryName = transaction.Category?.Name ?? "Без категории",
                    Description = transaction.Description ?? string.Empty,
                    CreatedAt = transaction.CreatedAt
                });
            }

            // Считаем итоги
            report.TotalIncome = transactions
                .Where(t => t.Type == OperationType.Income)
                .Sum(t => t.Amount);

            report.TotalExpense = transactions
                .Where(t => t.Type == OperationType.Expense)
                .Sum(t => t.Amount);

            return report;
        }
    }
}