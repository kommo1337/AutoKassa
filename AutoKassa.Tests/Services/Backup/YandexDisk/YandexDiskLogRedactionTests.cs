using AutoKassa.Services.Backup.YandexDisk;
using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// Регрессионная защита TRAPS §3.16: access-token, одноразовый код, code_verifier,
    /// Authorization-заголовок и error_description OAuth не должны попадать в Serilog-логи.
    /// Проверяются наблюдаемые побочные эффекты (события логера), а не структура запроса (YD-12).
    /// </summary>
    public class YandexDiskLogRedactionTests : IDisposable
    {
        private readonly CollectingSink _sink = new();
        private readonly ILogger _originalLogger;

        public YandexDiskLogRedactionTests()
        {
            // Временно перенаправляем глобальный логер в память; соседние тесты пишут туда же,
            // но маркеры уникальны (GUID), поэтому проверка отсутствия остаётся валидной.
            _originalLogger = Log.Logger;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(_sink)
                .CreateLogger();
        }

        public void Dispose()
        {
            Log.Logger = _originalLogger;
        }

        [Fact]
        public async Task OAuthCycle_DoesNotLeakTokenCodeVerifierOrErrorDescriptionToLogs()
        {
            var markerCode = $"code-{Guid.NewGuid():N}";
            var markerToken = $"tok-{Guid.NewGuid():N}";
            var markerDescription = $"descr-{Guid.NewGuid():N}";

            var store = new FakeYandexDiskTokenStore();
            var handler = new FakeHttpMessageHandler();
            var options = new YandexDiskOptions { ClientId = "log-test-client" };
            var service = new YandexDiskAuthService(options, store, new HttpClient(handler), TimeProvider.System, _ => { });

            // Полный успешный цикл: verifier генерируется и обменивается, токен сохраняется.
            await service.StartAuthorizationAsync();
            handler.Responses.Enqueue(TokenResponse(markerToken));
            await service.CompleteAuthorizationAsync(markerCode);

            // Отказ Яндекса: сервис логирует только поле error, без error_description.
            await service.StartAuthorizationAsync();
            handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { error = "invalid_grant", error_description = markerDescription }))
            });
            var act = () => service.CompleteAuthorizationAsync("any-code");
            await act.Should().ThrowAsync<InvalidOperationException>();

            AssertNoMarker(markerCode, "одноразового кода");
            AssertNoMarker(markerToken, "access-token");
            AssertNoMarker($"OAuth {markerToken}", "Authorization-заголовка");
            AssertNoMarker(markerDescription, "error_description");
        }

        [Fact]
        public async Task ApiErrors_DoNotLeakAuthorizationHeaderToLogs()
        {
            var markerToken = $"tok-{Guid.NewGuid():N}";
            var handler = new FakeHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            var options = new YandexDiskOptions { ClientId = "log-test-client", ApiBaseUrl = "https://cloud-api.test" };
            var client = new YandexDiskApiClient(new HttpClient(handler), options, new StubAuth(markerToken));

            try
            {
                await client.GetResourceAsync("app:/x.db");
            }
            catch (YandexDiskTransientException ex)
            {
                // Как в сервисе бэкапа: ошибка уходит в лог — исключение не должно нести токен.
                Log.Error(ex, "Облачный запрос не выполнен");
            }

            AssertNoMarker(markerToken, "access-token");
            AssertNoMarker($"OAuth {markerToken}", "Authorization-заголовка");
        }

        private void AssertNoMarker(string marker, string what)
        {
            _sink.Events
                .Select(RenderEvent)
                .Should().NotContain(
                    m => m.Contains(marker, StringComparison.Ordinal),
                    $"в логах не должно быть {what}");
        }

        private static string RenderEvent(LogEvent e)
        {
            var builder = new StringBuilder(e.RenderMessage());
            foreach (var property in e.Properties)
                builder.Append(' ').Append(property.Value.ToString());
            if (e.Exception != null)
                builder.Append(' ').Append(e.Exception);
            return builder.ToString();
        }

        private static HttpResponseMessage TokenResponse(string accessToken)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { access_token = accessToken, token_type = "bearer", expires_in = 3600 }))
            };

        private sealed class CollectingSink : ILogEventSink
        {
            public ConcurrentQueue<LogEvent> Events { get; } = new();
            public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
        }

        /// <summary>Auth-заглушка: выдаёт маркерный токен; заголовок формирует клиент.</summary>
        private sealed class StubAuth : IYandexDiskAuthService
        {
            private readonly string _token;

            public StubAuth(string token) => _token = token;

            public event Action? StateChanged { add { } remove { } }
            public Task<YandexDiskAuthState> GetStateAsync(CancellationToken ct = default)
                => Task.FromResult(YandexDiskAuthState.Connected);
            public bool IsClientIdConfigured() => true;
            public Task<YandexDiskAuthStart> StartAuthorizationAsync(CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task CompleteAuthorizationAsync(string code, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task CancelAuthorizationAsync(CancellationToken ct = default)
                => Task.CompletedTask;
            public Task DisconnectAsync(CancellationToken ct = default)
                => Task.CompletedTask;
            public Task<YandexDiskToken?> GetValidAccessTokenAsync(CancellationToken ct = default)
                => Task.FromResult<YandexDiskToken?>(new YandexDiskToken(_token, DateTime.UtcNow.AddHours(1)));
            public Task InvalidateTokenAsync(CancellationToken ct = default)
                => Task.CompletedTask;
        }
    }
}
