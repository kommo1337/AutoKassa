using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoKassa.Models
{
    /// <summary>
    /// Кредитная карта с отслеживанием лимита и долга
    /// </summary>
    [Table("CreditCards")]
    public class CreditCard
    {
        /// <summary>
        /// Уникальный идентификатор карты
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Пользовательское название карты
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Банк-эмитент (опционально)
        /// </summary>
        [MaxLength(100)]
        public string? BankName { get; set; }

        /// <summary>
        /// Кредитный лимит
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Limit { get; set; }

        /// <summary>
        /// Годовая процентная ставка, % (опционально)
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal? InterestRate { get; set; }

        /// <summary>
        /// День выписки по карте (1–31)
        /// </summary>
        [Range(1, 31)]
        public int? StatementDay { get; set; }

        /// <summary>
        /// День платежа по карте (1–31)
        /// </summary>
        [Range(1, 31)]
        public int? PaymentDay { get; set; }

        /// <summary>
        /// Дата последнего платежа (для расчёта следующего)
        /// </summary>
        public DateTime? LastPaymentDate { get; set; }

        /// <summary>
        /// Процент от текущего долга для минимального платежа
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MinimumPaymentPercent { get; set; }

        /// <summary>
        /// Начальный долг, введённый в настройках
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal InitialDebt { get; set; }

        /// <summary>
        /// Активна ли карта
        /// </summary>
        [Required]
        public bool IsActive { get; set; }

        /// <summary>
        /// Дата создания записи
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Покупки, совершённые по кредитной карте
        /// </summary>
        public virtual ICollection<CreditCardPurchase> Purchases { get; set; }

        /// <summary>
        /// Операции, привязанные к карте
        /// </summary>
        public virtual ICollection<Transaction> Transactions { get; set; }

        public CreditCard()
        {
            Purchases = new List<CreditCardPurchase>();
            Transactions = new List<Transaction>();
        }
    }
}
