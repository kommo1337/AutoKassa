using AutoKassa.Models;

namespace AutoKassa.Services.CreditCards
{
    /// <summary>
    /// Синхронизация настроек основной кредитной карты с таблицей <see cref="CreditCards"/>.
    /// </summary>
    public interface ICreditCardSettingsService
    {
        /// <summary>
        /// Синхронизирует поля кредитной карты из настроек.
        /// Создаёт карту с Id=1, если она отсутствует.
        /// </summary>
        /// <param name="settings">Текущие настройки приложения.</param>
        /// <param name="ct">Токен отмены.</param>
        Task SyncFromSettingsAsync(AppSettings settings, CancellationToken ct = default);
    }
}
