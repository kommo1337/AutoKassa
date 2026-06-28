using AutoKassa.Models.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoKassa.Models
{
    /// <summary>
    /// Финансовая операция (доход или расход)
    /// </summary>
    [Table("Transactions")]
    public class Transaction
    {
        /// <summary>
        /// Уникальный идентификатор операции
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Дата операции
        /// </summary>
        [Required]
        public DateTime Date { get; set; }

        /// <summary>
        /// Сумма операции в рублях
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Тип операции (Доход/Расход)
        /// </summary>
        [Required]
        public OperationType Type { get; set; }

        /// <summary>
        /// Тип оплаты (Наличные/Безналичные/Кредитная карта/Долг)
        /// </summary>
        [Required]
        public PaymentType PaymentType { get; set; } = PaymentType.Cash;

        /// <summary>
        /// ID категории операции
        /// </summary>
        [Required]
        public int CategoryId { get; set; }

        /// <summary>
        /// Навигационное свойство - категория операции
        /// </summary>
        [ForeignKey(nameof(CategoryId))]
        public virtual Category Category { get; set; } = null!;

        /// <summary>
        /// ID кредитной карты (для операций с типом оплаты "Кредитная карта")
        /// </summary>
        public int? CreditCardId { get; set; }

        /// <summary>
        /// Навигационное свойство - кредитная карта
        /// </summary>
        [ForeignKey(nameof(CreditCardId))]
        public virtual CreditCard? CreditCard { get; set; }

        /// <summary>
        /// ID контрагента (для долговых операций)
        /// </summary>
        public int? CounterpartyId { get; set; }

        /// <summary>
        /// Навигационное свойство - контрагент
        /// </summary>
        [ForeignKey(nameof(CounterpartyId))]
        public virtual Counterparty? Counterparty { get; set; }

        /// <summary>
        /// Статус долговой операции
        /// </summary>
        [Required]
        public DebtStatus DebtStatus { get; set; } = DebtStatus.NotDebt;

        /// <summary>
        /// Описание операции (опционально)
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Дата создания записи
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Дата последнего изменения записи
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Флаг мягкого удаления (Soft Delete)
        /// </summary>
        [Required]
        public bool IsDeleted { get; set; }
    }
}
