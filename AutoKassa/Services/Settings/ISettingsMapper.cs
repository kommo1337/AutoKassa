using AutoKassa.Models;
using AutoKassa.ViewModels;

namespace AutoKassa.Services.Settings
{
    /// <summary>
    /// Маппинг между моделью <see cref="AppSettings"/> и <see cref="SettingsViewModel"/>.
    /// </summary>
    public interface ISettingsMapper
    {
        /// <summary>
        /// Копирует значения из <paramref name="source"/> в публичные свойства <paramref name="target"/>.
        /// </summary>
        void MapToViewModel(AppSettings source, SettingsViewModel target);

        /// <summary>
        /// Копирует значения из VM в модель <paramref name="target"/>.
        /// </summary>
        void MapToModel(SettingsViewModel source, AppSettings target);

        /// <summary>
        /// Создаёт глубокую копию <paramref name="source"/>.
        /// </summary>
        AppSettings Clone(AppSettings source);
    }
}
