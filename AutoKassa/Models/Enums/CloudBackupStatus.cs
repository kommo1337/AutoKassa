namespace AutoKassa.Models.Enums
{
    /// <summary>
    /// Состояние облачного резервного копирования на Яндекс Диск
    /// </summary>
    public enum CloudBackupStatus
    {
        /// <summary>
        /// Аккаунт Яндекс Диска не подключён
        /// </summary>
        NotConnected = 0,

        /// <summary>
        /// Копия актуальна, выгружать не требуется
        /// </summary>
        UpToDate = 1,

        /// <summary>
        /// Подошёл срок очередной облачной копии
        /// </summary>
        Due = 2,

        /// <summary>
        /// Выгрузка выполняется прямо сейчас
        /// </summary>
        Uploading = 3,

        /// <summary>
        /// Выгрузка не удалась, файл оставлен в очереди для повтора
        /// </summary>
        PendingRetry = 4,

        /// <summary>
        /// Токен недействителен или истёк — требуется повторный вход
        /// </summary>
        AuthRequired = 5,

        /// <summary>
        /// Ошибка, требующая действия пользователя (квота, права и т.п.)
        /// </summary>
        Error = 6
    }
}
