using AutoKassa.Helpers;
using Serilog;
using System.IO;
using System.Text;

namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <inheritdoc />
    public sealed class YandexDiskClientIdStore : IYandexDiskClientIdStore
    {
        private static readonly ILogger _log = Log.ForContext<YandexDiskClientIdStore>();

        private static readonly string DefaultFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoKassa", "yandex-disk-clientid.txt");

        private readonly YandexDiskOptions _options;
        private readonly string _filePath;

        public YandexDiskClientIdStore(YandexDiskOptions options)
            : this(options, DefaultFilePath)
        {
        }

        // Внутренний конструктор с явным путём — для тестов с временными каталогами.
        internal YandexDiskClientIdStore(YandexDiskOptions options, string filePath)
        {
            _options = options;
            _filePath = filePath;
        }

        /// <summary>
        /// Применить сохранённый пользователем ClientId к загруженным опциям.
        /// Вызывается в корне композиции (App.ConfigureServices) до создания сервисов,
        /// зависящих от <see cref="YandexDiskOptions"/>.
        /// </summary>
        public static void ApplyStoredOverride(YandexDiskOptions options)
        {
            var stored = Read(DefaultFilePath);
            if (!string.IsNullOrWhiteSpace(stored))
                options.ClientId = stored;
        }

        public Task<string?> GetAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Read(_filePath));
        }

        public async Task SaveAsync(string clientId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var trimmed = (clientId ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                throw new InvalidOperationException("Введите ClientId приложения Яндекс Диска.");

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Атомарная запись через общий помощник — обрыв процесса не оставит битый файл.
            var content = Encoding.UTF8.GetBytes(trimmed + Environment.NewLine);
            await Task.Run(() => AtomicFile.WriteAllBytes(_filePath, content), ct).ConfigureAwait(false);

            // Живой singleton-экземпляр опций обновляем сразу — подключение не ждёт перезапуска.
            _options.ClientId = trimmed;
            _log.Information("ClientId Яндекс Диска сохранён пользователем");
        }

        private static string? Read(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var value = File.ReadAllText(filePath).Trim();
                return value.Length == 0 ? null : value;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Не удалось прочитать сохранённый ClientId Яндекс Диска");
                return null;
            }
        }
    }
}
