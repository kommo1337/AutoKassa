using System.Text.Json.Serialization;

namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Ответ токен-эндпоинта OAuth (https://oauth.yandex.ru/token).
    /// </summary>
    public sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        /// <summary>Срок действия в секундах с момента выдачи.</summary>
        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    /// <summary>
    /// Ответ GET /v1/disk/resources/upload — ссылка для загрузки файла.
    /// </summary>
    public sealed class DiskUploadLinkResponse
    {
        [JsonPropertyName("href")]
        public string? Href { get; set; }

        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("templated")]
        public bool Templated { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    /// <summary>
    /// Метаданные ресурса на Диске (фрагмент ответа GET /v1/disk/resources).
    /// </summary>
    public sealed class DiskResourceResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>MD5 содержимого (hex). Возвращается для файлов.</summary>
        [JsonPropertyName("md5")]
        public string? Md5 { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        /// <summary>Дата создания ресурса (ISO 8601).</summary>
        [JsonPropertyName("created")]
        public DateTime? Created { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    /// <summary>
    /// Содержимое папки в ответе GET /v1/disk/resources: элементы и общее число ресурсов.
    /// По документации API список лежит во вложенном объекте <c>_embedded</c>, а не на верхнем уровне.
    /// </summary>
    public sealed class DiskResourceEmbeddedResponse
    {
        [JsonPropertyName("items")]
        public List<DiskResourceResponse>? Items { get; set; }

        /// <summary>Общее число ресурсов в папке — контроль полноты постраничного чтения.</summary>
        [JsonPropertyName("total")]
        public int? Total { get; set; }
    }

    /// <summary>
    /// Ответ списка ресурсов (GET /v1/disk/resources с limit/offset).
    /// </summary>
    public sealed class DiskResourceListResponse
    {
        [JsonPropertyName("_embedded")]
        public DiskResourceEmbeddedResponse? Embedded { get; set; }

        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        [JsonPropertyName("offset")]
        public int? Offset { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }
}
