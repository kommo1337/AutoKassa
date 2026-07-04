using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoKassa.Models
{
    /// <summary>
    /// Связь между операцией-погашением кредитной карты и погашенной покупкой
    /// </summary>
    [Table("CreditCardPayments")]
    public class CreditCardPayment
    {
        /// <summary>
        /// Уникальный идентификатор записи
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Идентификатор операции-погашения
        /// </summary>
        [Required]
        public int RepaymentTransactionId { get; set; }

        /// <summary>
        /// Операция-погашение
        /// </summary>
        [ForeignKey(nameof(RepaymentTransactionId))]
        public virtual Transaction RepaymentTransaction { get; set; } = null!;

        /// <summary>
        /// Идентификатор покупки по кредитной карте
        /// </summary>
        [Required]
        public int CreditCardPurchaseId { get; set; }

        /// <summary>
        /// Покупка по кредитной карте
        /// </summary>
        [ForeignKey(nameof(CreditCardPurchaseId))]
        public virtual CreditCardPurchase CreditCardPurchase { get; set; } = null!;

        /// <summary>
        /// Сумма погашения, распределенная на данную покупку
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
    }
}
