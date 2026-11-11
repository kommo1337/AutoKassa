using AutoKassa.Models.Enums;

namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Метаданные файла в локальной очереди облачной выгрузки (раздел 6 аудита):
    /// имя, дата создания (UTC) и размер ожидающей копии для отображения в UI.
    /// </summary>
    /// <param name="FileName">Имя файла очереди (AutoKassa_Backup_*.db).</param>
    /// <param name="CreatedUtc">Дата создания файла (UTC).</param>
    /// <param name="SizeBytes">Размер в байтах.</param>
    public sealed record PendingQueueFileInfo(string FileName, DateTime CreatedUtc, long SizeBytes);

    /// <summary>
    /// Наблюдаемое состояние облачного резервного копирования.
    /// </summary>
    /// <param name="Status">Текущий статус.</param>
    /// <param name="LastSuccessUtc">Дата последней подтверждённой выгрузки (UTC) или null.</param>
    /// <param name="HasPendingFile">В локальной очереди остался незагруженный файл.</param>
    /// <param name="UserMessage">Понятное пользователю сообщение без технических деталей и секретов.</param>
    /// <param name="PendingFile">Метаданные ожидающего файла очереди или null, если очередь пуста.</param>
    /// <param name="LastFailureUtc">Время последней неудачной попытки (UTC) или null.</param>
    public sealed record CloudBackupState(
        CloudBackupStatus Status,
        DateTime? LastSuccessUtc,
        bool HasPendingFile,
        string? UserMessage,
        PendingQueueFileInfo? PendingFile = null,
        DateTime? LastFailureUtc = null);

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

        /// <summary>
        /// Удалить ожидающий файл из локальной очереди и сразу выполнить новую выгрузку
        /// (команда «Удалить ожидающую копию и создать новую», раздел 6 аудита). Закрывает
        /// сценарий постоянного 409 с чужим файлом тем же именем. Удаляется только производный
        /// файл очереди — исходная база данных не затрагивается.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        Task<CloudBackupResult> DiscardPendingAndUploadAsync(CancellationToken ct = default);
    }
}
