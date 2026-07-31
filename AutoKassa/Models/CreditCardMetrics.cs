namespace AutoKassa.Models
{
    /// <summary>
    /// Агрегированные метрики по кредитной карте.
    /// Возвращается пакетным методом <see cref="Services.ICreditCardService.GetCardsMetricsAsync"/>,
    /// чтобы не выполнять отдельные запросы (N+1) для каждой карты.
    /// </summary>
    public class CreditCardMetrics
    {
        /// <summary>
        /// Кредитная карта, к которой относятся метрики
        /// </summary>
        public CreditCard Card { get; set; } = null!;

        /// <summary>
        /// Текущий долг по карте
        /// </summary>
        public decimal CurrentDebt { get; set; }

        /// <summary>
        /// Доступный остаток кредитного лимита
        /// </summary>
        public decimal AvailableLimit { get; set; }

        /// <summary>
        /// Сумма минимального платежа
        /// </summary>
        public decimal MinimumPayment { get; set; }

        /// <summary>
        /// Дата ближайшего платежа
        /// </summary>
        public DateTime? NextPaymentDate { get; set; }
    }
}
