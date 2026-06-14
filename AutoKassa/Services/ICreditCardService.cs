using AutoKassa.Models;

namespace AutoKassa.Services
{
    /// <summary>
    /// Интерфейс сервиса для работы с кредитными картами
    /// </summary>
    public interface ICreditCardService
    {
        /// <summary>
        /// Получить список всех кредитных карт
        /// </summary>
        Task<IReadOnlyList<CreditCard>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Получить кредитную карту по ID
        /// </summary>
        Task<CreditCard?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Получить кредитную карту с историей покупок
        /// </summary>
        Task<CreditCard?> GetCardWithPurchasesAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Создать новую кредитную карту
        /// </summary>
        Task<CreditCard> CreateAsync(CreditCard card, CancellationToken ct = default);

        /// <summary>
        /// Обновить кредитную карту
        /// </summary>
        Task UpdateAsync(CreditCard card, CancellationToken ct = default);

        /// <summary>
        /// Деактивировать кредитную карту
        /// </summary>
        Task DeleteAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Получить текущий долг по карте
        /// </summary>
        Task<decimal> GetCurrentDebtAsync(int creditCardId, CancellationToken ct = default);

        /// <summary>
        /// Получить доступный остаток кредитного лимита
        /// </summary>
        Task<decimal> GetAvailableLimitAsync(int creditCardId, CancellationToken ct = default);

        /// <summary>
        /// Получить сумму минимального платежа
        /// </summary>
        Task<decimal> GetMinimumPaymentAsync(int creditCardId, CancellationToken ct = default);

        /// <summary>
        /// Получить дату ближайшего платежа
        /// </summary>
        Task<DateTime?> GetNextPaymentDateAsync(int creditCardId, CancellationToken ct = default);

        /// <summary>
        /// Получить общий текущий долг по всем активным картам
        /// </summary>
        Task<decimal> GetTotalDebtAsync(CancellationToken ct = default);

        /// <summary>
        /// Зарегистрировать покупку по кредитной карте
        /// </summary>
        Task AddPurchaseAsync(int creditCardId, int transactionId, decimal amount, CancellationToken ct = default);

        /// <summary>
        /// Зафиксировать погашение долга по кредитной карте
        /// </summary>
        Task RepayDebtAsync(int creditCardId, decimal amount, CancellationToken ct = default);
    }
}
