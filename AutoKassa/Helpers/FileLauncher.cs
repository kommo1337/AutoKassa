using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AutoKassa.Helpers
{
    /// <summary>
    /// Хелпер для безопасного открытия экспортированных файлов.
    /// Разрешает запуск только документов с известными расширениями (PDF, Excel)
    /// через ассоциированное приложение системы.
    /// </summary>
    public static class FileLauncher
    {
        private static readonly ILogger _log = Log.ForContext(typeof(FileLauncher));

        // Разрешённые расширения для экспортированных отчётов.
        private static readonly string[] AllowedExtensions = { ".pdf", ".xlsx" };

        /// <summary>
        /// Безопасно открыть экспортированный файл с проверкой расширения.
        /// </summary>
        /// <param name="filePath">Путь к файлу.</param>
        /// <returns>True, если файл успешно открыт; иначе false.</returns>
        public static bool OpenExportedFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _log.Warning("OpenExportedFile: путь к файлу пустой");
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(filePath);

                if (!File.Exists(fullPath))
                {
                    _log.Warning("OpenExportedFile: файл не найден {FilePath}", fullPath);
                    return false;
                }

                var extension = Path.GetExtension(fullPath);
                if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    _log.Warning("OpenExportedFile: запрещённое расширение файла {Extension} для {FilePath}", extension, fullPath);
                    return false;
                }

                var startInfo = new ProcessStartInfo(fullPath)
                {
                    UseShellExecute = true,
                    Verb = "open",
                    ErrorDialog = false
                };

                Process.Start(startInfo);
                _log.Information("OpenExportedFile: открыт файл {FilePath}", fullPath);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "OpenExportedFile: ошибка открытия файла {FilePath}", filePath);
                return false;
            }
        }
    }
}
