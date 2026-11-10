using Serilog;
using System.IO;

namespace AutoKassa.Helpers
{
    /// <summary>
    /// Атомарная запись файла: содержимое пишется во временный файл с уникальным именем
    /// в каталоге целевого файла, затем атомарно становится целевым (File.Replace/File.Move
    /// внутри одного тома). При сбое на любом шаге целевой файл остаётся прежним,
    /// временные файлы не накапливаются.
    /// </summary>
    public static class AtomicFile
    {
        private static readonly ILogger _log = Log.ForContext(typeof(AtomicFile));

        /// <summary>
        /// Атомарная замена <paramref name="targetPath"/> содержимым <paramref name="content"/>.
        /// </summary>
        /// <param name="replaceFile">
        /// Смена временного файла на целевой; по умолчанию — <see cref="Replace"/>.
        /// Параметр-сейм для тестов: позволяет имитировать сбой между записью временного
        /// файла и заменой и проверить, что прежнее содержимое целевого файла уцелело.
        /// </param>
        public static void WriteAllBytes(string targetPath, byte[] content, Action<string, string>? replaceFile = null)
        {
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Уникальное имя: параллельные записи не конфликтуют, аварийные остатки старого
            // запуска не перезаписываются чужим процессом до замены.
            var tempPath = Path.Combine(
                directory ?? string.Empty,
                $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

            var move = replaceFile ?? Replace;
            try
            {
                File.WriteAllBytes(tempPath, content);
                move(tempPath, targetPath);
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        /// <summary>Замена temp → target; File.Replace атомарен внутри одного тома Windows.</summary>
        public static void Replace(string tempPath, string targetPath)
        {
            if (File.Exists(targetPath))
                File.Replace(tempPath, targetPath, null);
            else
                File.Move(tempPath, targetPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Не удалось удалить временный файл {TempPath}", path);
            }
        }
    }
}
