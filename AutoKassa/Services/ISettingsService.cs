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

        /// <summary>
        /// Получить тип операции по умолчанию
        /// </summary>
        OperationType GetDefaultOperationType();

        /// <summary>
        /// Установить тип операции по умолчанию
        /// </summary>
        void SetDefaultOperationType(OperationType type);

        /// <summary>
        /// Получить ID категории по умолчанию для типа операции
        /// </summary>
        int? GetDefaultCategoryId(OperationType type);

        /// <summary>
        /// Установить категорию по умолчанию для типа операции
        /// </summary>
        void SetDefaultCategoryId(OperationType type, int? categoryId);

        #region Новые асинхронные методы

        /// <summary>
        /// Получить настройки асинхронно
        /// </summary>
        Task<AppSettings> GetSettingsAsync();

        /// <summary>
        /// Сохранить настройки асинхронно
        /// </summary>
        Task SaveSettingsAsync(AppSettings settings);

        /// <summary>
        /// Сбросить настройки к значениям по умолчанию
        /// </summary>
        Task ResetToDefaultsAsync();

        /// <summary>
        /// Экспортировать настройки в JSON файл
        /// </summary>
        /// <param name="filePath">Путь к файлу</param>
        /// <returns>true если экспорт успешен</returns>
        Task<bool> ExportSettingsAsync(string filePath);

        /// <summary>
        /// Импортировать настройки из JSON файла
        /// </summary>
        /// <param name="filePath">Путь к файлу</param>
        /// <returns>true если импорт успешен</returns>
        Task<bool> ImportSettingsAsync(string filePath);

        /// <summary>
        /// Создать резервную копию базы данных
        /// </summary>
        /// <param name="backupPath">Путь для сохранения</param>
        /// <returns>Путь к созданному файлу или null при ошибке</returns>
        Task<string?> CreateBackupAsync(string backupPath);

        /// <summary>
        /// Восстановить базу данных из резервной копии
        /// </summary>
        /// <param name="backupFilePath">Путь к файлу резервной копии</param>
        /// <returns>true если восстановление успешно</returns>
        Task<bool> RestoreBackupAsync(string backupFilePath);

        /// <summary>
        /// Запустить авто-бэкап, если подошёл срок (проверяет BackupEnabled и AutoBackupDays)
        /// </summary>
        Task RunAutoBackupIfDueAsync();

        #endregion
    }
}