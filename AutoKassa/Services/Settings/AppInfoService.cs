using AutoKassa.Models;
using AutoKassa.Services;
using System.IO;
using System.Reflection;

namespace AutoKassa.Services.Settings
{
    /// <summary>
    /// Реализация загрузки информации «О программе».
    /// </summary>
    public class AppInfoService : IAppInfoService
    {
        private readonly ISettingsService _settingsService;
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="AppInfoService"/>.
        /// </summary>
        public AppInfoService(
            ISettingsService settingsService,
            ITransactionService transactionService,
            ICategoryService categoryService)
        {
            _settingsService = settingsService;
            _transactionService = transactionService;
            _categoryService = categoryService;
        }

        /// <inheritdoc />
        public async Task<AppInfo> GetAppInfoAsync(CancellationToken ct = default)
        {
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

            var dbPath = await _settingsService.GetDatabasePathAsync().ConfigureAwait(false);
            string dbFileSize;
            if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
            {
                var size = new FileInfo(dbPath).Length;
                dbFileSize = size < 1024 * 1024
                    ? $"{size / 1024.0:F1} КБ"
                    : $"{size / (1024.0 * 1024.0):F1} МБ";
            }
            else
            {
                dbFileSize = "—";
            }

            var totalTransactions = await _transactionService
                .GetTotalCountAsync(new TransactionFilterParameters(), ct)
                .ConfigureAwait(false);

            var allCategories = await _categoryService.GetAllAsync().ConfigureAwait(false);
            var totalCategories = allCategories.Count;

            var lastBackup = await _settingsService.GetLastBackupDateAsync().ConfigureAwait(false);
            var lastBackupDate = lastBackup?.ToString("dd.MM.yyyy HH:mm") ?? "Нет данных";

            return new AppInfo(appVersion, dbFileSize, totalTransactions, totalCategories, lastBackupDate);
        }
    }
}
