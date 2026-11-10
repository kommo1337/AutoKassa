using System.IO;
using System.Text.Json;

namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Публичные настройки интеграции с Яндекс Диском (без секретов).
    /// Загружаются из appsettings.json (секция "YandexDisk") рядом с исполняемым файлом.
    /// </summary>
    public class YandexDiskOptions
    {
        // Стандартные URL OAuth/API Яндекс Диска. Переопределяются в appsettings.json только
        // для тестовых стендов; в поставке приложения используются дефолтные значения.
        public const string DefaultAuthorizeUrl = "https://oauth.yandex.ru/authorize";
        public const string DefaultTokenUrl = "https://oauth.yandex.ru/token";
        public const string DefaultApiBaseUrl = "https://cloud-api.yandex.net";
        public const string DefaultRedirectUri = "https://oauth.yandex.ru/verification_code";

        /// <summary>
        /// Публичный client_id приложения из кабинета разработчика Яндекса.
        /// Пустая строка = облачный бэкап не настроен (кнопка подключения должна сообщить об этом).
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>URL страницы авторизации OAuth.</summary>
        public string AuthorizeUrl { get; set; } = DefaultAuthorizeUrl;

        /// <summary>URL обмена одноразового кода на access-token.</summary>
        public string TokenUrl { get; set; } = DefaultTokenUrl;

        /// <summary>Базовый URL REST API Диска.</summary>
        public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;

        /// <summary>Redirect URI для потока с вводом кода вручную.</summary>
        public string RedirectUri { get; set; } = DefaultRedirectUri;

        /// <summary>Запрашиваемое право доступа — только папка приложения (app:/).</summary>
        public string Scope { get; set; } = "cloud_api:disk.app_folder";

        /// <summary>
        /// Лимит одного API-запроса (не загрузки файла). По умолчанию 100 с —
        /// прежнее значение общего HttpClient.Timeout. Таймаут превращается в
        /// <see cref="YandexDiskTransientException"/> и получает повторы, а не считается отменой.
        /// </summary>
        public TimeSpan ApiRequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

        /// <summary>
        /// Лимит загрузки файла: большая БД на медленной сети может идти дольше обычного
        /// API-запроса. По умолчанию 10 минут; не отключается полностью.
        /// </summary>
        public TimeSpan UploadTimeout { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Загрузить опции из appsettings.json в каталоге приложения.
        /// Отсутствующий файл или секция возвращают дефолты (ClientId = пусто).
        /// </summary>
        public static YandexDiskOptions Load(string? baseDirectory = null)
        {
            var options = new YandexDiskOptions();
            var configPath = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(configPath))
                return options;

            try
            {
                using var stream = File.OpenRead(configPath);
                using var document = JsonDocument.Parse(stream);
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("YandexDisk", out var section) &&
                    section.ValueKind == JsonValueKind.Object)
                {
                    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var loaded = JsonSerializer.Deserialize<YandexDiskOptions>(section.GetRawText(), jsonOptions);
                    if (loaded != null)
                        options = loaded;
                }
            }
            catch (Exception ex)
            {
                // Некорректный appsettings.json не должен ломать запуск приложения —
                // облачный бэкап просто останется с дефолтными (пустыми) опциями.
                Serilog.Log.Warning(ex, "Не удалось прочитать секцию YandexDisk из {ConfigPath}", configPath);
            }

            return options;
        }
    }
}
