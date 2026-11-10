using AutoKassa.Models.Enums;

namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Наблюдаемое состояние облачного резервного копирования.
    /// </summary>
    /// <param name="Status">Текущий статус.</param>
    /// <param name="LastSuccessUtc">Дата последней подтверждённой выгрузки (UTC) или null.</param>
    /// <param name="HasPendingFile">В локальной очереди остался незагруженный файл.</param>
    /// <param name="UserMessage">Понятное пользователю сообщение без технических деталей и секретов.</param>
    public sealed record CloudBackupState(
        CloudBackupStatus Status,
        DateTime? LastSuccessUtc,
        bool HasPendingFile,
        string? UserMessage);

    /// <summary>
    /// Результат одной попытки облачного копирования.
    /// </summary>
    /// <param name="Success">Копия подтверждена на Диске (или выгрузка не требовалась).</param>
    /// <param name="State">Актуальное состояние после попытки.</param>
    public sealed record CloudBackupResult(bool Success, CloudBackupState State);

    /// <summary>
    /// Сервис выгрузки SQLite-снимка в папку приложения на Яндекс Диске (app:/).
    ///
    /// Сценарий: переиспользовать файл из локальной очереди (%LOCALAPPDATA%\AutoKassa\CloudBackupQueue)
    /// или создать новый снимок через ISettingsService.CreateSqliteSnapshotAsync; проверить снимок
    /// (PRAGMA integrity_check, размер, MD5); загрузить без перезаписи; подтвердить через метаданные
    /// Диска; после подтверждения удалить файл очереди и выполнить ротацию облачных копий.
    /// При сетевых сбоях файл остаётся в очереди и переиспользуется следующей попыткой.
    ///
    /// Событие <see cref="StateChanged"/> поднимается на потоке операции — подписчики из UI
    /// обязаны маршалить на свой SynchronizationContext (это делает SettingsViewModel).
    /// </summary>
    public interface IYandexDiskBackupService
    {
        /// <summary>Текущее состояние (до первой операции — NotConnected).</summary>
        CloudBackupState CurrentState { get; }

        /// <summary>Событие изменения состояния.</summary>
        event Action<CloudBackupState>? StateChanged;

        /// <summary>
        /// Выполнить цикл облачного копирования.
        /// </summary>
        /// <param name="force">
        /// true — загрузить немедленно, игнорируя срок AutoBackupDays (команда «Загрузить сейчас»).
        /// false — выгружать только если подошёл срок или в очереди остался незагруженный файл.
        /// </param>
        /// <param name="ct">Токен отмены.</param>
        Task<CloudBackupResult> UploadNowAsync(bool force = false, CancellationToken ct = default);
    }
}
