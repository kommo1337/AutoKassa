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

        public ReportService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Сформировать отчет "Баланс за период"
        /// </summary>
        public async Task<BalanceReport> GenerateBalanceReportAsync(DateTime dateFrom, DateTime dateTo)
        {
            var report = new BalanceReport
            {
                DateFrom = dateFrom,
                DateTo = dateTo
            };

            // Получаем начальный баланс
            report.StartBalance = await GetInitialBalanceAsync(dateFrom);

            // Получаем операции за период
            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.Date >= dateFrom && t.Date <= dateTo)
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
        public async Task<decimal> GetInitialBalanceAsync(DateTime date)
        {
            var incomeBeforeDate = await _context.Transactions
                .Where(t => t.Type == OperationType.Income && t.Date < date)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            var expenseBeforeDate = await _context.Transactions
                .Where(t => t.Type == OperationType.Expense && t.Date < date)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            return incomeBeforeDate - expenseBeforeDate;
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
    }
}