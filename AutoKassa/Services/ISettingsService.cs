using AutoKassa.Models;
using AutoKassa.Models.Enums;

namespace AutoKassa.Services
{
    /// <summary>
    /// Интерфейс сервиса для работы с настройками приложения
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Получить настройки приложения
        /// </summary>
        AppSettings GetSettings();

        /// <summary>
        /// Сохранить настройки приложения
        /// </summary>
        void SaveSettings(AppSettings settings);

        /// <summary>
        /// Проверить, установлен ли пароль
        /// </summary>
        bool IsPasswordSet();

        /// <summary>
        /// Установить пароль
        /// </summary>
        void SetPassword(string passwordHash, SecurityQuestion? questionId, string answerHash, string customQuestion = null);

        /// <summary>
        /// Обновить пароль
        /// </summary>
        void UpdatePassword(string newPasswordHash);

        /// <summary>
        /// Получить таймаут автоблокировки (в минутах)
        /// </summary>
        int GetAutoLockTimeout();

        /// <summary>
        /// Установить таймаут автоблокировки
        /// </summary>
        void SetAutoLockTimeout(int minutes);

        /// <summary>
        /// Получить текущую тему
        /// </summary>
        string GetTheme();

        /// <summary>
        /// Установить тему
        /// </summary>
        void SetTheme(string theme);
    }
}