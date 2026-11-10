using Serilog;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Реализация REST-клиента Яндекс Диска на встроенном HttpClient.
    /// Access-token запрашивается у <see cref="IYandexDiskAuthService"/> для каждого вызова;
    /// токен и тела ответов с ошибками OAuth никогда не логируются.
    /// </summary>
    public sealed class YandexDiskApiClient : IYandexDiskApiClient
    {
        private static readonly ILogger _log = Log.ForContext<YandexDiskApiClient>();

        // Размер страницы при чтении содержимого app:/ (максимум API — 1000, берём меньше для стабильности).
        private const int ResourceListPageSize = 100;

        // Читаем только нужные поля: контракт ротации опирается на name/type/size/md5/created и total.
        private const string ResourceFields = "name,md5,size,created,type";
        private const string ResourceListFields =
            "_embedded.items.name,_embedded.items.size,_embedded.items.md5,_embedded.items.created,_embedded.items.type,_embedded.total";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = false
        };

        private readonly HttpClient _httpClient;
        private readonly YandexDiskOptions _options;
        private readonly IYandexDiskAuthService _authService;

        public YandexDiskApiClient(HttpClient httpClient, YandexDiskOptions options, IYandexDiskAuthService authService)
        {
            _httpClient = httpClient;
            _options = options;
            _authService = authService;
        }

        /// <inheritdoc />
        public async Task<DiskUploadLink> GetUploadLinkAsync(string remotePath, bool overwrite, CancellationToken ct = default)
        {
            var url = string.Concat(
                _options.ApiBaseUrl,
                "/v1/disk/resources/upload?path=", Uri.EscapeDataString(remotePath),
                "&overwrite=", overwrite ? "true" : "false");

            using var response = await SendWithAuthAsync(HttpMethod.Get, url, ct).ConfigureAwait(false);
            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

            var payload = await ReadJsonAsync<DiskUploadLinkResponse>(response, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(payload.Href))
                throw new YandexDiskApiException("Яндекс Диск вернул пустую ссылку для загрузки файла.");

            return new DiskUploadLink(payload.Href, payload.Method ?? "PUT");
        }

        /// <inheritdoc />
        public async Task<YandexDiskUploadResult> UploadFileAsync(string href, string localFilePath, CancellationToken ct = default)
        {
            await using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Загрузка идёт на выданный сервером href с авторизацией через query-параметры ссылки,
            // поэтому общий OAuth-заголовок здесь не нужен и не добавляется.
            using var request = new HttpRequestMessage(HttpMethod.Put, href)
            {
                Content = new StreamContent(stream)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            HttpResponseMessage response;
            // Таймаут загрузки — отдельный, щедрый (большая БД на медленной сети);
            // общий HttpClient.Timeout отключён, лимит применяем здесь через связанный токен.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_options.UploadTimeout);
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                // Таймаут от нашего CancelAfter: внешний токен не отменён — это повторяемый сбой, а не отмена операции.
                throw new YandexDiskTransientException("Превышено время загрузки файла на Яндекс Диск.", ex);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                throw new YandexDiskTransientException("Сетевая ошибка при загрузке файла на Яндекс Диск.", ex);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.Created)
                    return YandexDiskUploadResult.Created;
                if (response.StatusCode == HttpStatusCode.Accepted)
                    return YandexDiskUploadResult.Accepted;

                await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

                // Документированные коды — 201/202; сюда попадаем только при неожиданном 2xx.
                _log.Warning("Неожиданный код ответа при загрузке файла: {StatusCode}", response.StatusCode);
                return YandexDiskUploadResult.Created;
            }
        }

        /// <inheritdoc />
        public async Task<DiskResourceInfo> GetResourceAsync(string remotePath, CancellationToken ct = default)
        {
            var url = string.Concat(
                _options.ApiBaseUrl,
                "/v1/disk/resources?path=", Uri.EscapeDataString(remotePath),
                "&fields=", Uri.EscapeDataString(ResourceFields));

            using var response = await SendWithAuthAsync(HttpMethod.Get, url, ct).ConfigureAwait(false);
            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

            var payload = await ReadJsonAsync<DiskResourceResponse>(response, ct).ConfigureAwait(false);
            return ToResourceInfo(payload);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DiskResourceInfo>> GetAppFolderResourcesAsync(CancellationToken ct = default)
        {
            var result = new List<DiskResourceInfo>();
            var offset = 0;

            while (true)
            {
                var url = string.Concat(
                    _options.ApiBaseUrl,
                    "/v1/disk/resources?path=app%3A%2F", // app:/
                    "&limit=", ResourceListPageSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "&offset=", offset.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "&fields=", Uri.EscapeDataString(ResourceListFields));

                using var response = await SendWithAuthAsync(HttpMethod.Get, url, ct).ConfigureAwait(false);
                await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

                var payload = await ReadJsonAsync<DiskResourceListResponse>(response, ct).ConfigureAwait(false);
                // Реальный API отдаёт список во вложенном объекте _embedded, а не на верхнем уровне.
                var items = payload.Embedded?.Items ?? new List<DiskResourceResponse>();

                foreach (var item in items)
                {
                    result.Add(ToResourceInfo(item));
                }

                offset += items.Count;

                // Частичная (или пустая) страница — последняя. Полная страница, но total уже достигнут —
                // тоже завершение, дальше запрашивать нечего.
                if (items.Count < ResourceListPageSize)
                    break;
                if (payload.Embedded?.Total is int total && result.Count >= total)
                    break;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task DeleteResourceAsync(string remotePath, CancellationToken ct = default)
        {
            var url = string.Concat(
                _options.ApiBaseUrl,
                "/v1/disk/resources?path=", Uri.EscapeDataString(remotePath),
                "&permanently=true");

            using var response = await SendWithAuthAsync(HttpMethod.Delete, url, ct).ConfigureAwait(false);

            // 204 — удалено синхронно, 202 — операция поставлена в очередь.
            if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.Accepted)
                return;

            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> SendWithAuthAsync(HttpMethod method, string url, CancellationToken ct)
        {
            var token = await _authService.GetValidAccessTokenAsync(ct).ConfigureAwait(false)
                ?? throw new YandexDiskAuthException("Нет действительного токена Яндекс Диска — требуется повторный вход.");

            using var request = new HttpRequestMessage(method, url);
            // Заголовок формируется на каждый запрос отдельно и никогда не логируется.
            request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", token.AccessToken);

            // Лимит API-запроса — наш собственный (общий HttpClient.Timeout отключён).
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_options.ApiRequestTimeout);
            try
            {
                return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                // Таймаут запроса при живом внешнем токене — повторяемый сбой, а не штатная отмена.
                throw new YandexDiskTransientException("Превышено время ожидания ответа от Яндекс Диска.", ex);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                throw new YandexDiskTransientException("Сетевая ошибка при обращении к Яндекс Диску.", ex);
            }
        }

        private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode)
                return;

            // Тело ошибки не логируем: в служебных полях Яндекса могут быть токены и пути.
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    throw new YandexDiskAuthException("Яндекс Диск отклонил токен (HTTP 401) — требуется повторный вход.");
                case HttpStatusCode.NotFound:
                    throw new YandexDiskNotFoundException("Яндекс Диск вернул HTTP 404: ресурс не найден.");
                case HttpStatusCode.Conflict:
                    throw new YandexDiskConflictException("Файл с таким именем уже существует на Яндекс Диске (HTTP 409).");
                case HttpStatusCode.Forbidden:
                case (HttpStatusCode)507: // Insufficient Storage
                    throw new YandexDiskQuotaException("Недостаточно места или прав на Яндекс Диске.");
                case HttpStatusCode.TooManyRequests:
                    throw new YandexDiskTransientException("Превышен лимит запросов к Яндекс Диску (HTTP 429).", GetRetryAfter(response));
                default:
                    var code = (int)response.StatusCode;
                    if (code >= 500)
                        throw new YandexDiskTransientException($"Яндекс Диск временно недоступен (HTTP {(int)response.StatusCode}).", GetRetryAfter(response));

                    var detail = await TryReadErrorAsync(response, ct).ConfigureAwait(false);
                    throw new YandexDiskApiException($"Яндекс Диск вернул ошибку HTTP {(int)response.StatusCode}: {detail}");
            }
        }

        private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
        {
            return response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date.HasValue == true
                    ? response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow
                    : null);
        }

        private static async Task<string> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
        {
            try
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("error_description", out var description) &&
                    description.ValueKind == JsonValueKind.String)
                    return description.GetString() ?? "без описания";
                if (document.RootElement.TryGetProperty("error", out var error) &&
                    error.ValueKind == JsonValueKind.String)
                    return error.GetString() ?? "без описания";
                return "без описания";
            }
            catch (Exception)
            {
                return "без описания";
            }
        }

        private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                ?? throw new YandexDiskApiException("Яндекс Диск вернул пустой или некорректный ответ.");
        }

        private static DiskResourceInfo ToResourceInfo(DiskResourceResponse payload)
        {
            if (string.IsNullOrEmpty(payload.Name))
                throw new YandexDiskApiException("Яндекс Диск вернул метаданные без имени ресурса.");

            return new DiskResourceInfo(
                payload.Name,
                payload.Size ?? 0,
                payload.Md5,
                payload.Created?.ToUniversalTime(),
                payload.Type);
        }
    }
}
