namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Фасадный сервис для загрузки и агрегации данных дашборда.
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Загружает полный набор данных для указанного периода.
        /// </summary>
        Task<DashboardData> LoadDashboardDataAsync(
            DateTime dateFrom,
            DateTime dateTo,
            CancellationToken ct = default);
    }
}
