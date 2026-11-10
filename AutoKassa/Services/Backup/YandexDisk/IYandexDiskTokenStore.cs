namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Защищённое хранилище access-token Яндекс Диска.
    /// Реализация обязана шифровать данные при записи на диск; токен никогда не попадает в логи.
    /// </summary>
    public interface IYandexDiskTokenStore
    {
        /// <summary>Прочитать сохранённый токен. null, если токена нет или файл повреждён.</summary>
        Task<YandexDiskToken?> GetAsync(CancellationToken ct = default);

        /// <summary>Атомарно сохранить токен.</summary>
        Task SaveAsync(YandexDiskToken token, CancellationToken ct = default);

        /// <summary>Удалить сохранённый токен (отключение аккаунта).</summary>
        Task ClearAsync(CancellationToken ct = default);
    }
}
