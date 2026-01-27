using System;
using System.Collections.Generic;
using AutoKassa.Models.Enums;

namespace AutoKassa.Models.Reports
{
    /// <summary>
    /// Отчет "Детализация операций"
    /// </summary>
    public class TransactionDetailReport : ReportBase
    {
        public TransactionDetailReport()
        {
            Name = "Детализация операций";
            Transactions = new List<TransactionDetailItem>();
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
        /// Тип операции для фильтра (null = все)
        /// </summary>
        public OperationType? FilterType { get; set; }

        /// <summary>
        /// ID категории для фильтра (null = все)
        /// </summary>
        public int? FilterCategoryId { get; set; }

        /// <summary>
        /// Название категории для фильтра
        /// </summary>
        public string FilterCategoryName { get; set; }

        /// <summary>
        /// Список операций
        /// </summary>
        public List<TransactionDetailItem> Transactions { get; set; }

        /// <summary>
        /// Общая сумма доходов
        /// </summary>
        public decimal TotalIncome { get; set; }

        /// <summary>
        /// Общая сумма расходов
        /// </summary>
        public decimal TotalExpense { get; set; }

        /// <summary>
        /// Разница (доходы - расходы)
        /// </summary>
        public decimal NetAmount => TotalIncome - TotalExpense;

        /// <summary>
        /// Количество операций
        /// </summary>
        public int TransactionCount => Transactions.Count;
    }

    /// <summary>
    /// Детальная информация об операции для отчета
    /// </summary>
    public class TransactionDetailItem
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public OperationType Type { get; set; }
        public string TypeName => Type == OperationType.Income ? "Доход" : "Расход";
        public decimal Amount { get; set; }
        public string CategoryName { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
