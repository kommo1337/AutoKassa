namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Хранение ClientId приложения Яндекс Диска, введённого пользователем.
    /// ClientId не является секретом (он передаётся в URL авторизации открыто),
    /// поэтому хранится в открытом виде в профиле пользователя и имеет приоритет
    /// над значением из appsettings.json — портативная установка может лежать
    /// в каталоге без прав записи.
    /// </summary>
    public interface IYandexDiskClientIdStore
    {
        /// <summary>Сохранённый пользователем ClientId или null, если не вводился.</summary>
        Task<string?> GetAsync(CancellationToken ct = default);

        /// <summary>
        /// Сохранить ClientId и применить его к живому экземпляру <see cref="YandexDiskOptions"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Пустой или состоящий из пробелов ClientId.</exception>
        Task SaveAsync(string clientId, CancellationToken ct = default);
    }
}
