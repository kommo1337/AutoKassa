namespace AutoKassa.Services.Backup
{
    /// <summary>
    /// Координатор операций резервного копирования и восстановления БД с UI-диалогами.
    /// </summary>
    public interface IBackupCoordinator
    {
        /// <summary>
        /// Показывает диалог выбора папки.
        /// </summary>
        /// <param name="initialPath">Начальная директория или null.</param>
        /// <returns>Выбранный путь или null, если пользователь отменил выбор.</returns>
        string? SelectBackupFolder(string? initialPath = null);

        /// <summary>
        /// Создаёт резервную копию БД по указанному пути.
        /// Если путь пуст — сначала показывает диалог выбора папки.
        /// </summary>
        /// <param name="targetPath">Путь для сохранения копии.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Путь к созданному файлу или null при ошибке/отмене.</returns>
        Task<string?> CreateBackupAsync(string? targetPath, CancellationToken ct = default);

        /// <summary>
        /// Восстанавливает БД из файла резервной копии с диалогом выбора файла.
        /// При успехе перезапускает приложение.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        Task RestoreBackupAsync(CancellationToken ct = default);
    }
}
