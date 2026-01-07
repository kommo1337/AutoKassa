using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoKassa.Models.Enums;

namespace AutoKassa.Models
{
    /// <summary>
    /// Категория финансовых операций
    /// </summary>
    [Table("Categories")]
    public class Category
    {
        /// <summary>
        /// Уникальный идентификатор категории
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Название категории
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        /// <summary>
        /// Тип категории (Доход/Расход)
        /// </summary>
        [Required]
        public OperationType Type { get; set; }

        /// <summary>
        /// Активность категории (для деактивации вместо удаления)
        /// </summary>
        [Required]
        public bool IsActive { get; set; }

        /// <summary>
        /// Признак системной категории (предустановленная, нельзя удалить)
        /// </summary>
        [Required]
        public bool IsSystem { get; set; }

        /// <summary>
        /// Дата создания категории
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Навигационное свойство - операции данной категории
        /// </summary>
        public virtual ICollection<Transaction> Transactions { get; set; }

        public Category()
        {
            Transactions = new List<Transaction>();
        }
    }
}