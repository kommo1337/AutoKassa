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

        public ReportService(
            IDbContextFactory<AppDbContext> contextFactory,
            ISettingsService settingsService,
            ICreditCardService creditCardService)
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
                .Where(t => !t.IsDeleted && t.Date >= dateFrom.Date && t.Date < dateToEnd && t.PaymentType != PaymentType.Debt);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);

            // SQL-агрегация итогов вместо загрузки всех транзакций в память.
            // Долговые операции не влияют на прибыль и фактический баланс.
            var totals = await query
                .Where(t => t.PaymentType != PaymentType.Debt)
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            report.TotalIncome  = totals.FirstOrDefault(r => r.Type == OperationType.Income)?.Total  ?? 0m;
            report.TotalExpense = totals.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m;
            report.EndBalance   = report.StartBalance + report.TotalIncome - report.TotalExpense;

            // Активные долги для дашборда
            report.TotalDebtReceivable = await GetActiveDebtAsync(context, OperationType.Income, ct).ConfigureAwait(false);
            report.TotalDebtPayable = await GetActiveDebtAsync(context, OperationType.Expense, ct).ConfigureAwait(false);

            // Кредитные покупки и обязательства
            report.TotalCreditPurchases = await GetCreditPurchasesAsync(context, dateFrom, dateToEnd, paymentType, ct).ConfigureAwait(false);
            report.TotalCreditDebt = await _creditCardService.GetTotalDebtAsync(ct).ConfigureAwait(false);
            report.FactBalance = await GetFactBalanceAsync(dateFrom, dateTo, paymentType, ct).ConfigureAwait(false);
            report.NetBalance = report.FactBalance - report.TotalCreditDebt;

            // Ближайший платёж по кредитным картам
            var (nextPaymentDate, nextPaymentAmount) = await GetNextCreditPaymentAsync(ct).ConfigureAwait(false);
            report.NextCreditPaymentDate = nextPaymentDate;
            report.NextCreditPaymentAmount = nextPaymentAmount;

            // SQL-агрегация по дням (долги исключаем)
            var dailyRows = await query
                .Where(t => t.PaymentType != PaymentType.Debt)
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
                .Where(t => !t.IsDeleted && t.Date < date.Date && t.PaymentType != PaymentType.Debt);
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

            // SQL-агрегация итогов (долги исключаем)
            var totals = await query
                .Where(t => t.PaymentType != PaymentType.Debt)
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            report.TotalIncome  = totals.FirstOrDefault(r => r.Type == OperationType.Income)?.Total  ?? 0m;
            report.TotalExpense = totals.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m;

            return report;
        }

        /// <summary>
        /// Сформировать отчёт по долгам
        /// </summary>
        public async Task<DebtReport> GenerateDebtReportAsync(
            DateTime? dateFrom,
            DateTime? dateTo,
            OperationType? direction,
            DebtStatus? status,
            CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var from = dateFrom ?? DateTime.MinValue;
            var to = dateTo ?? DateTime.MaxValue;
            var dateToEnd = to.Date.AddDays(1);

            var query = context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.Counterparty)
                .Where(t => !t.IsDeleted && t.PaymentType == PaymentType.Debt && t.DebtStatus != DebtStatus.NotDebt)
                .Where(t => t.Date >= from.Date && t.Date < dateToEnd);

            if (direction.HasValue)
                query = query.Where(t => t.Type == direction.Value);

            if (status.HasValue)
                query = query.Where(t => t.DebtStatus == status.Value);

            var debts = await query
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var debtIds = debts.Select(d => d.Id).ToList();

            var repaidAmounts = await context.DebtPayments
                .AsNoTracking()
                .Where(dp => debtIds.Contains(dp.DebtTransactionId))
                .Where(dp => !dp.RepaymentTransaction.IsDeleted)
                .GroupBy(dp => dp.DebtTransactionId)
                .Select(g => new { DebtId = g.Key, Total = (decimal)g.Sum(dp => (double)dp.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var repaidDict = repaidAmounts.ToDictionary(r => r.DebtId, r => r.Total);

            var items = debts.Select(t => new DebtItem
            {
                TransactionId = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                RepaidAmount = repaidDict.GetValueOrDefault(t.Id),
                Status = t.DebtStatus,
                Direction = t.Type,
                CounterpartyId = t.CounterpartyId,
                CounterpartyName = t.Counterparty?.Name ?? "—",
                CounterpartyType = t.Counterparty?.Type ?? CounterpartyType.Other,
                CategoryName = t.Category?.Name ?? "—",
                Description = t.Description
            }).ToList();

            var report = new DebtReport
            {
                DateFrom = from,
                DateTo = to,
                Items = items,
                TotalReceivable = items.Where(i => i.Direction == OperationType.Income && i.Status != DebtStatus.WrittenOff).Sum(i => i.Amount),
                TotalPayable = items.Where(i => i.Direction == OperationType.Expense && i.Status != DebtStatus.WrittenOff).Sum(i => i.Amount),
                ActiveReceivable = items.Where(i => i.Direction == OperationType.Income && i.Status == DebtStatus.Active).Sum(i => i.RemainingAmount),
                ActivePayable = items.Where(i => i.Direction == OperationType.Expense && i.Status == DebtStatus.Active).Sum(i => i.RemainingAmount)
            };

            return report;
        }

        /// <summary>
        /// Получить сумму активных долгов указанного направления
        /// </summary>
        private async Task<decimal> GetActiveDebtAsync(
            AppDbContext context,
            OperationType direction,
            CancellationToken ct)
        {
            var debtIds = await context.Transactions
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.PaymentType == PaymentType.Debt && t.Type == direction && t.DebtStatus == DebtStatus.Active)
                .Select(t => t.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (debtIds.Count == 0)
                return 0m;

            var totalDebt = await context.Transactions
                .AsNoTracking()
                .Where(t => debtIds.Contains(t.Id))
                .Select(t => (double?)t.Amount)
                .SumAsync(ct)
                .ConfigureAwait(false) ?? 0;

            var totalRepaid = await context.DebtPayments
                .AsNoTracking()
                .Where(dp => debtIds.Contains(dp.DebtTransactionId) && !dp.RepaymentTransaction.IsDeleted)
                .Select(dp => (double?)dp.Amount)
                .SumAsync(ct)
                .ConfigureAwait(false) ?? 0;

            return (decimal)totalDebt - (decimal)totalRepaid;
        }

        /// <summary>
        /// Получить данные для окна "Сверка кассы" на указанную дату.
        /// Отображаются остатки на конец даты (как на дашборде), а не оборот за один день.
        /// </summary>
        public async Task<ReconciliationData> GetReconciliationDataAsync(DateTime date, CancellationToken ct = default)
        {
            // Остатки на конец выбранной даты.
            // GetInitialBalanceAsync возвращает баланс на начало даты,
            // поэтому передаём date.AddDays(1), чтобы получить остаток на конец даты.
            var cashAmount = await GetInitialBalanceAsync(date.AddDays(1), PaymentType.Cash, ct).ConfigureAwait(false);
            var nonCashAmount = await GetInitialBalanceAsync(date.AddDays(1), PaymentType.NonCash, ct).ConfigureAwait(false);

            // Кредитный долг и ближайший платёж
            var creditDebt = await _creditCardService.GetTotalDebtAsync(ct).ConfigureAwait(false);
            var (nextPaymentDate, nextPaymentAmount) = await GetNextCreditPaymentAsync(ct).ConfigureAwait(false);

            return new ReconciliationData
            {
                Date = date,
                CashAmount = cashAmount,
                NonCashAmount = nonCashAmount,
                CreditDebt = creditDebt,
                NextPaymentDate = nextPaymentDate,
                NextPaymentAmount = nextPaymentAmount
            };
        }

        /// <summary>
        /// Получить ближайший платёж по кредитным картам и сумму платежей на эту дату
        /// </summary>
        private async Task<(DateTime? Date, decimal Amount)> GetNextCreditPaymentAsync(CancellationToken ct)
        {
            var cards = await _creditCardService.GetAllAsync(ct).ConfigureAwait(false);
            var activeCards = cards.Where(c => c.IsActive).ToList();
            if (activeCards.Count == 0)
                return (null, 0m);

            var cardPayments = await Task.WhenAll(activeCards.Select(async card => new
            {
                Date = await _creditCardService.GetNextPaymentDateAsync(card.Id, ct).ConfigureAwait(false),
                Amount = await _creditCardService.GetMinimumPaymentAsync(card.Id, ct).ConfigureAwait(false)
            })).ConfigureAwait(false);

            var upcoming = cardPayments
                .Where(x => x.Date.HasValue)
                .OrderBy(x => x.Date)
                .FirstOrDefault();

            if (upcoming == null)
                return (null, 0m);

            var amount = cardPayments
                .Where(x => x.Date == upcoming.Date)
                .Sum(x => x.Amount);

            return (upcoming.Date!.Value, amount);
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
