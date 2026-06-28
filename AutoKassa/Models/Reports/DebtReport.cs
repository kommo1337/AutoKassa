using System;
using System.Collections.Generic;

namespace AutoKassa.Models.Reports
{
    /// <summary>
    /// Отчёт по долгам (дебиторская / кредиторская задолженность)
    /// </summary>
    public class DebtReport : ReportBase
    {
        public DebtReport()
        {
            Name = "Отчёт по долгам";
            Items = new List<DebtItem>();
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
        /// Общая сумма, которую нам должны (все активные долги-доходы)
        /// </summary>
        public decimal TotalReceivable { get; set; }

        /// <summary>
        /// Общая сумма, которую мы должны (все активные долги-расходы)
        /// </summary>
        public decimal TotalPayable { get; set; }

        /// <summary>
        /// Активная дебиторская задолженность (не погашена)
        /// </summary>
        public decimal ActiveReceivable { get; set; }

        /// <summary>
        /// Активная кредиторская задолженность (не погашена)
        /// </summary>
        public decimal ActivePayable { get; set; }

        /// <summary>
        /// Баланс (нам должны минус мы должны)
        /// </summary>
        public decimal Balance => ActiveReceivable - ActivePayable;

        /// <summary>
        /// Список долгов
        /// </summary>
        public List<DebtItem> Items { get; set; }
    }
}
