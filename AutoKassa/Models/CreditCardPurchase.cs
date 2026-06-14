using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoKassa.Models
{
    /// <summary>
    /// Покупка, оплаченная кредитной картой
    /// </summary>
    [Table("CreditCardPurchases")]
    public class CreditCardPurchase
    {
        /// <summary>
        /// Уникальный идентификатор покупки
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID кредитной карты
        /// </summary>
        [Required]
        public int CreditCardId { get; set; }

        /// <summary>
        /// Навигационное свойство - кредитная карта
        /// </summary>
        [ForeignKey(nameof(CreditCardId))]
        public virtual CreditCard CreditCard { get; set; } = null!;

        /// <summary>
        /// ID связанной операции-расхода
        /// </summary>
        [Required]
        public int TransactionId { get; set; }

        /// <summary>
        /// Навигационное свойство - операция
        /// </summary>
        [ForeignKey(nameof(TransactionId))]
        public virtual Transaction Transaction { get; set; } = null!;

        /// <summary>
        /// Сумма покупки
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Оставшийся долг по этой покупке
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingDebt { get; set; }

        /// <summary>
        /// Дата покупки
        /// </summary>
        [Required]
        public DateTime PurchaseDate { get; set; }

        /// <summary>
        /// Примечание (опционально)
        /// </summary>
        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
