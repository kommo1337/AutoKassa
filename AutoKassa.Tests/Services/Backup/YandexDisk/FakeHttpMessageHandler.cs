using System.Net;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>Записанный запрос для последующих проверок.</summary>
    public sealed record RecordedHttpRequest(string Method, string Url, string? Body, string? Authorization);

    /// <summary>
    /// HttpMessageHandler с записью запросов. Ответ берётся из очереди <see cref="Responses"/>,
    /// если она пуста — из функции, переданной в конструктор (по умолчанию 200 {}).
    /// Функции передаётся CancellationToken запроса — для тестов таймаутов.
    /// </summary>
    public sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? responder = null)
        {
            _responder = responder ?? ((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            }));
        }

        /// <summary>Очередь ответов: каждый SendAsync забирает первый элемент.</summary>
        public Queue<HttpResponseMessage> Responses { get; } = new();

        public List<RecordedHttpRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedHttpRequest(
                request.Method.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                body,
                request.Headers.Authorization?.ToString()));
            return Responses.Count > 0 ? Responses.Dequeue() : await _responder(request, cancellationToken);
        }
    }
}
