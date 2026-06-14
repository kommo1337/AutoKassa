using System;
using System.Collections.Generic;

namespace AutoKassa.Models.Reports
{
    /// <summary>
    /// Отчет "Баланс за период"
    /// </summary>
    public class BalanceReport : ReportBase
    {
        public BalanceReport()
        {
            Name = "Баланс за период";
            DailyBalances = new List<DailyBalance>();
        }

        /// <summary>
        /// Период с
        /// </summary>
        public DateTime DateFrom { get; set; }

        /// <summary>
        /// Период по
        /// </summary>
        public DateTime DateTo { get; set; }

        /// <summary>
        /// Начальный баланс
        /// </summary>
        public decimal StartBalance { get; set; }

        /// <summary>
        /// Конечный баланс
        /// </summary>
        public decimal EndBalance { get; set; }

        /// <summary>
        /// Общая сумма доходов
        /// </summary>
        public decimal TotalIncome { get; set; }

        /// <summary>
        /// Общая сумма расходов
        /// </summary>
        public decimal TotalExpense { get; set; }

        /// <summary>
        /// Сумма кредитных покупок за период
        /// </summary>
        public decimal TotalCreditPurchases { get; set; }

        /// <summary>
        /// Текущий общий долг по кредитным картам
        /// </summary>
        public decimal TotalCreditDebt { get; set; }

        /// <summary>
        /// Фактический баланс (наличные + безналичные)
        /// </summary>
        public decimal FactBalance { get; set; }

        /// <summary>
        /// Условный баланс с учётом кредитного долга
        /// </summary>
        public decimal NetBalance { get; set; }

        /// <summary>
        /// Прибыль
        /// </summary>
        public decimal Profit => TotalIncome - TotalExpense;

        /// <summary>
        /// Данные по дням для графика
        /// </summary>
        public List<DailyBalance> DailyBalances { get; set; }
    }

    /// <summary>
    /// Данные баланса за один день
    /// </summary>
    public class DailyBalance
    {
        public DateTime Date { get; set; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Balance { get; set; }
    }
}