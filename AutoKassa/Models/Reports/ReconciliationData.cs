using System;

namespace AutoKassa.Models.Reports
{
    /// <summary>
    /// Данные для окна «Сверка кассы»
    /// </summary>
    public class ReconciliationData
    {
        /// <summary>
        /// Дата сверки
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Остаток наличных на конец даты
        /// </summary>
        public decimal CashAmount { get; set; }

        /// <summary>
        /// Остаток безналичных на конец даты
        /// </summary>
        public decimal NonCashAmount { get; set; }

        /// <summary>
        /// Общий текущий долг по кредитным картам
        /// </summary>
        public decimal CreditDebt { get; set; }

        /// <summary>
        /// Сумма минимального платежа на ближайшую дату
        /// </summary>
        public decimal NextPaymentAmount { get; set; }

        /// <summary>
        /// Дата ближайшего платежа по кредитным картам
        /// </summary>
        public DateTime? NextPaymentDate { get; set; }
    }
}
