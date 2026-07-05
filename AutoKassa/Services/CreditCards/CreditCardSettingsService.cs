using AutoKassa.Models;
using AutoKassa.Services;

namespace AutoKassa.Services.CreditCards
{
    /// <summary>
    /// Реализация синхронизации настроек основной кредитной карты.
    /// </summary>
    public class CreditCardSettingsService : ICreditCardSettingsService
    {
        private readonly ICreditCardService _creditCardService;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="CreditCardSettingsService"/>.
        /// </summary>
        public CreditCardSettingsService(ICreditCardService creditCardService)
        {
            _creditCardService = creditCardService;
        }

        /// <inheritdoc />
        public async Task SyncFromSettingsAsync(AppSettings settings, CancellationToken ct = default)
        {
            var existing = await _creditCardService.GetByIdAsync(1, ct).ConfigureAwait(false);
            if (existing != null)
            {
                existing.Name = "Основная кредитная карта";
                existing.Limit = settings.CreditCardLimit;
                existing.InitialDebt = settings.CreditCardCurrentDebt;
                existing.InterestRate = settings.CreditCardInterestRate;
                existing.PaymentDay = settings.CreditCardPaymentDay;
                existing.LastPaymentDate = settings.CreditCardLastPaymentDate;
                existing.MinimumPaymentPercent = settings.CreditCardMinimumPaymentPercent;
                existing.IsActive = true;
                await _creditCardService.UpdateAsync(existing, ct).ConfigureAwait(false);
            }
            else
            {
                var card = new CreditCard
                {
                    Name = "Основная кредитная карта",
                    Limit = settings.CreditCardLimit,
                    InitialDebt = settings.CreditCardCurrentDebt,
                    InterestRate = settings.CreditCardInterestRate,
                    PaymentDay = settings.CreditCardPaymentDay,
                    LastPaymentDate = settings.CreditCardLastPaymentDate,
                    MinimumPaymentPercent = settings.CreditCardMinimumPaymentPercent,
                    IsActive = true
                };
                await _creditCardService.CreateAsync(card, ct).ConfigureAwait(false);
            }
        }
    }
}
