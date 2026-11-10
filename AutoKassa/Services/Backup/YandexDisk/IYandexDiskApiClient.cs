namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Результат загрузки файла на выданную сервером ссылку.
    /// </summary>
    public enum YandexDiskUploadResult
    {
        /// <summary>HTTP 201 — ресурс создан и уже доступен.</summary>
        Created = 0,

        /// <summary>HTTP 202 — загрузка принята, ресурс появится с небольшой задержкой.</summary>
        Accepted = 1
    }

    /// <summary>
    /// Краткие метаданные ресурса на Диске (имя, размер, MD5, дата создания, тип).
    /// <see cref="Type"/> — значение поля <c>type</c> API (<c>file</c>/<c>dir</c>); null, если API его не вернул.
    /// </summary>
    public sealed record DiskResourceInfo(string Name, long Size, string? Md5, DateTime? CreatedUtc, string? Type);

    /// <summary>
    /// Ссылка для загрузки, выданная GET /v1/disk/resources/upload.
    /// </summary>
    public sealed record DiskUploadLink(string Href, string Method);

    /// <summary>
    /// Тонкий REST-клиент Яндекс Диска для папки приложения (app:/).
    /// OAuth-заголовок формируется отдельно для каждого запроса;
    /// разбор ошибок сопоставляет HTTP-коды с типизированными исключениями.
    /// </summary>
    public interface IYandexDiskApiClient
    {
        /// <summary>
        /// Получить ссылку для загрузки файла. При <paramref name="overwrite"/> = false
        /// существующий ресурс приводит к <see cref="YandexDiskConflictException"/> (HTTP 409).
        /// </summary>
        Task<DiskUploadLink> GetUploadLinkAsync(string remotePath, bool overwrite, CancellationToken ct = default);

        /// <summary>Отправить локальный файл методом PUT на выданную ссылку.</summary>
        Task<YandexDiskUploadResult> UploadFileAsync(string href, string localFilePath, CancellationToken ct = default);

        /// <summary>Прочитать метаданные ресурса (имя, размер, MD5, дата создания).</summary>
        Task<DiskResourceInfo> GetResourceAsync(string remotePath, CancellationToken ct = default);

        /// <summary>Прочитать все страницы содержимого папки приложения (app:/).</summary>
        Task<IReadOnlyList<DiskResourceInfo>> GetAppFolderResourcesAsync(CancellationToken ct = default);

        /// <summary>
        /// Удалить ресурс в папке приложения. Используется только ротацией
        /// для файлов с управляемым именем AutoKassa_Backup_*.db.
        /// </summary>
        Task DeleteResourceAsync(string remotePath, CancellationToken ct = default);
    }
}
