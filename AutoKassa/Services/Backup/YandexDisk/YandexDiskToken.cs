namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Access-token Яндекс Диска с моментом истечения срока действия.
    /// Refresh-token намеренно не хранится: Яндекс требует client_secret для refresh grant,
    /// а общий секрет нельзя встраивать в десктопное приложение.
    /// </summary>
    public sealed record YandexDiskToken(string AccessToken, DateTime ExpiresAtUtc);
}
