using System;
using System.Collections.Generic;

namespace AutoKassa.Models
{
    /// <summary>
    /// Базовый класс для всех отчетов
    /// </summary>
    public abstract class ReportBase
    {
        /// <summary>
        /// Название отчета
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Дата формирования отчета
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        protected ReportBase()
        {
            GeneratedAt = DateTime.Now;
        }
    }
}