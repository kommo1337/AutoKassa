using AutoKassa.ViewModels;

namespace AutoKassa.Services.Settings
{
    /// <summary>
    /// Валидация значений полей настроек.
    /// </summary>
    public interface ISettingsValidator
    {
        /// <summary>
        /// Проверяет значения в VM и возвращает список сообщений об ошибках.
        /// Если ошибок нет — возвращается пустая коллекция.
        /// </summary>
        IReadOnlyList<string> Validate(SettingsViewModel vm);
    }
}
