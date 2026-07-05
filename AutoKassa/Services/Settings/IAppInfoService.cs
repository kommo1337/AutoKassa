namespace AutoKassa.Services.Settings
{
    /// <summary>
    /// Загрузка информации «О программе».
    /// </summary>
    public interface IAppInfoService
    {
        /// <summary>
        /// Загружает актуальную информацию о приложении.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        Task<AppInfo> GetAppInfoAsync(CancellationToken ct = default);
    }
}
