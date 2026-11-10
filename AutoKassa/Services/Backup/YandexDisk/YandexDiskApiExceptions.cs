namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Базовое исключение REST-клиента Яндекс Диска.
    /// </summary>
    public class YandexDiskApiException : Exception
    {
        public YandexDiskApiException(string message) : base(message)
        {
        }

        public YandexDiskApiException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// HTTP 401 — токен недействителен или истёк: требуется повторный вход.
    /// </summary>
    public sealed class YandexDiskAuthException : YandexDiskApiException
    {
        public YandexDiskAuthException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// HTTP 404 — ресурс ещё не появился (например, после загрузки с ответом 202)
    /// или был удалён. Отдельный тип, чтобы ожидание ресурса отличать от ошибок.
    /// </summary>
    public sealed class YandexDiskNotFoundException : YandexDiskApiException
    {
        public YandexDiskNotFoundException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// HTTP 409 — ресурс уже существует и перезапись запрещена.
    /// </summary>
    public sealed class YandexDiskConflictException : YandexDiskApiException
    {
        public YandexDiskConflictException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// HTTP 403/507 и другие ошибки квоты/прав: требуют действия пользователя,
    /// частые повторы бессмысленны.
    /// </summary>
    public sealed class YandexDiskQuotaException : YandexDiskApiException
    {
        public YandexDiskQuotaException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Временная ошибка (сеть, HTTP 429, 5xx): попытку можно повторить.
    /// </summary>
    public sealed class YandexDiskTransientException : YandexDiskApiException
    {
        /// <summary>Рекомендованная сервером задержка перед повтором или null.</summary>
        public TimeSpan? RetryAfter { get; }

        public YandexDiskTransientException(string message, TimeSpan? retryAfter = null)
            : base(message)
        {
            RetryAfter = retryAfter;
        }

        public YandexDiskTransientException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
