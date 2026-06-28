using AutoKassa.Models.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoKassa.Models
{
    /// <summary>
    /// Контрагент (клиент, филиал, поставщик и т.д.)
    /// </summary>
    [Table("Counterparties")]
    public class Counterparty
    {
        /// <summary>
        /// Уникальный идентификатор контрагента
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Название контрагента
        /// </summary>
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Тип контрагента
        /// </summary>
        [Required]
        public CounterpartyType Type { get; set; }

        /// <summary>
        /// Номер телефона контрагента
        /// </summary>
        [MaxLength(20)]
        public string? Phone { get; set; }

        /// <summary>
        /// Примечания к контрагенту
        /// </summary>
        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// Признак активности контрагента
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Дата создания записи
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Операции, связанные с контрагентом
        /// </summary>
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
