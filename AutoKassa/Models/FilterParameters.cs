using System;
using AutoKassa.Models.Enums;

namespace AutoKassa.Models
{
    /// <summary>
    /// Параметры фильтрации операций
    /// </summary>
    public class TransactionFilterParameters
    {
        /// <summary>
        /// Дата начала периода
        /// </summary>
        public DateTime? DateFrom { get; set; }

        /// <summary>
        /// Дата окончания периода
        /// </summary>
        public DateTime? DateTo { get; set; }

        /// <summary>
        /// Тип операции (null = все)
        /// </summary>
        public OperationType? Type { get; set; }

        /// <summary>
        /// ID категории (null = все)
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Поиск по описанию
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        /// Количество записей для пропуска (для пагинации)
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// Количество записей для загрузки
        /// </summary>
        public int Take { get; set; } = 100;

        /// <summary>
        /// Поле для сортировки
        /// </summary>
        public string SortBy { get; set; } = "Date";

        /// <summary>
        /// Направление сортировки (true = по убыванию)
        /// </summary>
        public bool SortDescending { get; set; } = true;
    }
}