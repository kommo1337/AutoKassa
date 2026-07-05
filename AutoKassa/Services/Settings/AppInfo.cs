namespace AutoKassa.Services.Settings
{
    /// <summary>
    /// Информация о приложении для вкладки «О программе».
    /// </summary>
    public record AppInfo(
        string AppVersion,
        string DbFileSize,
        int TotalTransactions,
        int TotalCategories,
        string LastBackupDate);
}
