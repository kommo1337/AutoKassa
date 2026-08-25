using AutoKassa.Services;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace AutoKassa.Services.Backup
{
    /// <summary>
    /// Реализация координатора резервного копирования и восстановления БД.
    /// </summary>
    public class BackupCoordinator : IBackupCoordinator
    {
        private readonly IDialogService _dialogService;
        private readonly ISettingsService _settingsService;
        private readonly IToastNotificationService _toastService;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="BackupCoordinator"/>.
        /// </summary>
        public BackupCoordinator(
            IDialogService dialogService,
            ISettingsService settingsService,
            IToastNotificationService toastService)
        {
            _dialogService = dialogService;
            _settingsService = settingsService;
            _toastService = toastService;
        }

        /// <inheritdoc />
        public string? SelectBackupFolder(string? initialPath = null)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Выберите папку для резервных копий",
                FileName = "Выберите эту папку",
                Filter = "Папка|*.folder",
                CheckPathExists = true,
                CheckFileExists = false
            };

            if (!string.IsNullOrEmpty(initialPath))
                dialog.InitialDirectory = initialPath;

            if (dialog.ShowDialog() == true)
                return Path.GetDirectoryName(dialog.FileName);

            return null;
        }

        /// <inheritdoc />
        public async Task<string?> CreateBackupAsync(string? targetPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                targetPath = SelectBackupFolder();
                if (string.IsNullOrWhiteSpace(targetPath))
                    return null;
            }

            try
            {
                var backupFile = await _settingsService.CreateBackupAsync(targetPath);

                if (!string.IsNullOrEmpty(backupFile))
                {
                    _toastService.ShowSuccess($"Резервная копия создана:\n{backupFile}");
                    return backupFile;
                }

                _dialogService.ShowError("Не удалось создать резервную копию");
                return null;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка создания резервной копии в {TargetPath}", targetPath);
                _dialogService.ShowError("Не удалось создать резервную копию. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task RestoreBackupAsync(CancellationToken ct = default)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Файлы базы данных (*.db)|*.db",
                Title = "Выберите файл резервной копии"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var success = await _settingsService.RestoreBackupAsync(dialog.FileName);

                if (success)
                {
                    _dialogService.ShowInfo("База данных восстановлена. Приложение будет перезапущено.");
                    Process.Start(Environment.ProcessPath!);
                    Application.Current.Shutdown();
                }
                else
                {
                    _dialogService.ShowError("Не удалось восстановить базу данных");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка восстановления базы данных из {FileName}", dialog.FileName);
                _dialogService.ShowError("Не удалось восстановить базу данных. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
        }
    }
}
