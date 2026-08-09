using System;
using System.Collections.Generic;
using AutoKassa.Models.Enums;

namespace AutoKassa.Models.Reports
{
    /// <summary>
    /// Отчет "Структура по категориям"
    /// </summary>
    public class CategoryReport
    {
        public CategoryReport()
        {
            CategoryItems = new List<CategoryReportItem>();
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
        /// Тип операций (Доход/Расход)
        /// </summary>
        public OperationType OperationType { get; set; }

        /// <summary>
        /// Общая сумма по всем категориям
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Количество операций
        /// </summary>
        public int TransactionCount { get; set; }

        /// <summary>
        /// Данные по категориям
        /// </summary>
        public List<CategoryReportItem> CategoryItems { get; set; }
    }

    /// <summary>
    /// Данные по одной категории в отчете
    /// </summary>
    public class CategoryReportItem
    {
        /// <summary>
        /// ID категории
        /// </summary>
        public int CategoryId { get; set; }

        /// <summary>
        /// Название категории
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// Сумма по категории
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Процент от общей суммы
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Количество операций в категории
        /// </summary>
        public int TransactionCount { get; set; }

        /// <summary>
        /// Цвет для диаграммы (HEX)
        /// </summary>
        public string Color { get; set; }
    }
}
