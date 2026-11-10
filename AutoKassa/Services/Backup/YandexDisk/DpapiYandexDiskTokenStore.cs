using AutoKassa.Helpers;
using Serilog;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Хранение токена Яндекс Диска в файле %LOCALAPPDATA%\AutoKassa\Secrets\yandex-disk-token.bin.
    /// Содержимое сериализуется в JSON и шифруется DPAPI (ProtectedData.CurrentUser):
    /// расшифровать его может только тот Windows-пользователь, который записал файл,
    /// и только на той же машине/учётной записи.
    ///
    /// В лог пишутся только факты операций (сохранено/прочитано/повреждено), никогда — токен.
    /// </summary>
    public sealed class DpapiYandexDiskTokenStore : IYandexDiskTokenStore
    {
        private static readonly ILogger _log = Log.ForContext<DpapiYandexDiskTokenStore>();

        // Энтропия лишь помечает шифротекст как принадлежащий этому приложению (защита от
        // случайного копирования между программами). Она НЕ защищает от вредоносного процесса
        // того же Windows-пользователя: строка статична и извлекается из приложения.
        // Реальная защита здесь — привязка DPAPI CurrentUser к профилю пользователя и машине.
        private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("AutoKassa.YandexDisk.Token.v1");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

        private readonly string _filePath;
        // Смена temp → target вынесена в делегат (сейм для тестов): имитация сбоя между
        // записью временного файла и заменой проверяет, что прежний токен уцелел (YD-08).
        private readonly Action<string, string>? _replaceFile;

        public DpapiYandexDiskTokenStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoKassa", "Secrets", "yandex-disk-token.bin"))
        {
        }

        // Внутренний конструктор с явным путем — для тестов с временными каталогами.
        internal DpapiYandexDiskTokenStore(string filePath, Action<string, string>? replaceFile = null)
        {
            _filePath = filePath;
            _replaceFile = replaceFile;
        }

        public async Task<YandexDiskToken?> GetAsync(CancellationToken ct = default)
        {
            if (!File.Exists(_filePath))
                return null;

            try
            {
                var protectedBytes = await File.ReadAllBytesAsync(_filePath, ct).ConfigureAwait(false);
                var jsonBytes = ProtectedData.Unprotect(protectedBytes, s_entropy, DataProtectionScope.CurrentUser);
                var payload = JsonSerializer.Deserialize<TokenPayload>(jsonBytes);
                if (payload == null || string.IsNullOrEmpty(payload.AccessToken) || payload.ExpiresAtUtc == default)
                {
                    _log.Warning("Файл токена Яндекс Диска не содержит данных");
                    return null;
                }

                return new YandexDiskToken(payload.AccessToken, payload.ExpiresAtUtc);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Повреждённый/чужой файл (например, другой Windows-пользователь) — не раскрываем данные,
                // просто сообщаем, что токена нет: пользователь выполнит повторный вход.
                _log.Warning(ex, "Не удалось прочитать сохранённый токен Яндекс Диска (файл повреждён или недоступен)");
                return null;
            }
        }

        public async Task SaveAsync(YandexDiskToken token, CancellationToken ct = default)
        {
            var payload = new TokenPayload { AccessToken = token.AccessToken, ExpiresAtUtc = token.ExpiresAtUtc };
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            var protectedBytes = ProtectedData.Protect(jsonBytes, s_entropy, DataProtectionScope.CurrentUser);

            // Атомарная запись через общий помощник: временный файл с уникальным именем,
            // File.Replace — без окна, в котором сохранённого токена нет (YD-08).
            await Task.Run(() => AtomicFile.WriteAllBytes(_filePath, protectedBytes, _replaceFile), ct).ConfigureAwait(false);
            _log.Information("Токен Яндекс Диска сохранён (срок действия до {ExpiresAtUtc})", token.ExpiresAtUtc);
        }

        public Task ClearAsync(CancellationToken ct = default)
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
                _log.Information("Сохранённый токен Яндекс Диска удалён");
            }
            return Task.CompletedTask;
        }

        private sealed class TokenPayload
        {
            public string AccessToken { get; set; } = string.Empty;
            public DateTime ExpiresAtUtc { get; set; }
        }
    }
}
