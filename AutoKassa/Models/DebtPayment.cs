using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoKassa.Models
{
    /// <summary>
    /// Связь между долговой операцией и операцией-погашением
    /// </summary>
    [Table("DebtPayments")]
    public class DebtPayment
    {
        /// <summary>
        /// Уникальный идентификатор записи
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Идентификатор долговой операции
        /// </summary>
        [Required]
        public int DebtTransactionId { get; set; }

        /// <summary>
        /// Долговая операция
        /// </summary>
        [ForeignKey(nameof(DebtTransactionId))]
        public virtual Transaction DebtTransaction { get; set; } = null!;

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
        /// Сумма погашения
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
    }
}
