using System;
using System.Collections.Generic;
using AutoKassa.Models.Reports;

namespace AutoKassa.Helpers
{
    /// <summary>
    /// Группа долгов за один день для группированного отображения в отчёте.
    /// </summary>
    public class DebtReportGroup
    {
        /// <summary>
        /// Дата группы.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Подпись даты ("Сегодня", "Вчера" или полная дата).
        /// </summary>
        public string DateLabel { get; set; } = "";

        /// <summary>
        /// Сумма активных долгов "нам должны" за день.
        /// </summary>
        public decimal DayReceivable { get; set; }

        /// <summary>
        /// Сумма активных долгов "мы должны" за день.
        /// </summary>
        public decimal DayPayable { get; set; }

        /// <summary>
        /// Элементы долгов за день.
        /// </summary>
        public List<DebtItem> Items { get; set; } = new();
    }
}
