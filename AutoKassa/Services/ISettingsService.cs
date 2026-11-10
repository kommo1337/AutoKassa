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
        /// Получить настройки приложения (из кеша, без I/O при предзагрузке)
        /// </summary>
        AppSettings GetSettings();

        /// <summary>
        /// Проверить, установлен ли пароль
        /// </summary>
        bool IsPasswordSet();

        /// <summary>
        /// Получить таймаут автоблокировки (в минутах)
        /// </summary>
        int GetAutoLockTimeout();

        /// <summary>
        /// Получить текущую тему
        /// </summary>
        string GetTheme();

        /// <summary>
        /// Получить тип операции по умолчанию
        /// </summary>
        OperationType GetDefaultOperationType();

        /// <summary>
        /// Получить ID категории по умолчанию для типа операции
        /// </summary>
        Task<int?> GetDefaultCategoryIdAsync(OperationType type);

        /// <summary>
        /// Установить пароль
        /// </summary>
        Task SetPasswordAsync(string passwordHash, SecurityQuestion? questionId, string answerHash, string customQuestion = null);

        /// <summary>
        /// Обновить пароль
        /// </summary>
        Task UpdatePasswordAsync(string newPasswordHash);

        /// <summary>
        /// Установить таймаут автоблокировки
        /// </summary>
        Task SetAutoLockTimeoutAsync(int minutes);

        /// <summary>
        /// Установить тему
        /// </summary>
        Task SetThemeAsync(string theme);

        /// <summary>
        /// Установить тип операции по умолчанию
        /// </summary>
        Task SetDefaultOperationTypeAsync(OperationType type);

        /// <summary>
        /// Установить категорию по умолчанию для типа операции
        /// </summary>
        Task SetDefaultCategoryIdAsync(OperationType type, int? categoryId);

        #region Асинхронные методы

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
        /// Создать резервную копию базы данных (снимок + локальная ротация сверх BackupKeepCount).
        /// Ошибки и отмена превращаются в null — контракт для локального сценария;
        /// для облачного снимка без побочных эффектов используйте <see cref="CreateSqliteSnapshotAsync"/>.
        /// </summary>
        /// <param name="backupPath">Путь для сохранения</param>
        /// <param name="ct">Токен отмены</param>
        /// <returns>Путь к созданному файлу или null при ошибке/отмене</returns>
        Task<string?> CreateBackupAsync(string backupPath, CancellationToken ct = default);

        /// <summary>
        /// Создать согласованный SQLite-снимок базы (SQLite Online Backup API) в указанном каталоге.
        /// Общая часть <see cref="CreateBackupAsync"/> без локальной ротации и глотания ошибок:
        /// <see cref="InvalidOperationException"/> — файл базы данных не найден;
        /// <see cref="OperationCanceledException"/> — отмена; IOException и прочие — ошибки записи.
        /// </summary>
        Task<string> CreateSqliteSnapshotAsync(string targetDirectory, CancellationToken ct = default);

        /// <summary>
        /// Восстановить базу данных из резервной копии
        /// </summary>
        /// <param name="backupFilePath">Путь к файлу резервной копии</param>
        /// <param name="ct">Токен отмены</param>
        /// <returns>true если восстановление успешно</returns>
        Task<bool> RestoreBackupAsync(string backupFilePath, CancellationToken ct = default);

        /// <summary>
        /// Запустить авто-бэкап, если подошёл срок (проверяет BackupEnabled и AutoBackupDays)
        /// </summary>
        /// <param name="ct">Токен отмены</param>
        Task RunAutoBackupIfDueAsync(CancellationToken ct = default);

        /// <summary>
        /// Зафиксировать дату последней успешной облачной выгрузки (UTC).
        /// Точечный сеттер для фоновых сервисов — не мутирует кешированный объект снаружи сервиса.
        /// </summary>
        /// <param name="value">Дата успеха (UTC) или null (сброс, например при новом подключении).</param>
        /// <param name="ct">Токен отмены</param>
        Task SetYandexDiskLastBackupUtcAsync(DateTime? value, CancellationToken ct = default);

        /// <summary>
        /// Получить дату последнего резервного копирования (async)
        /// </summary>
        Task<DateTime?> GetLastBackupDateAsync();

        /// <summary>
        /// Получить путь к файлу базы данных (async)
        /// </summary>
        Task<string?> GetDatabasePathAsync();

        #endregion
    }
}