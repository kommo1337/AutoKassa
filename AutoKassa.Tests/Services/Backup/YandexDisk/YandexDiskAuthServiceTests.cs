using AutoKassa.Services.Backup.YandexDisk;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    public class YandexDiskAuthServiceTests
    {
        private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeYandexDiskTokenStore _store;
        private readonly FakeHttpMessageHandler _handler;
        private readonly FakeTimeProvider _time;
        private readonly YandexDiskOptions _options;
        private string? _openedUrl;
        private int _stateChangedCount;

        public YandexDiskAuthServiceTests()
        {
            _store = new FakeYandexDiskTokenStore();
            _handler = new FakeHttpMessageHandler();
            _time = new FakeTimeProvider(StartTime);
            _options = new YandexDiskOptions
            {
                ClientId = "test-client",
                AuthorizeUrl = "https://oauth.yandex.ru/authorize",
                TokenUrl = "https://oauth.yandex.ru/token"
            };
        }

        private YandexDiskAuthService CreateService()
        {
            var service = new YandexDiskAuthService(_options, _store, new HttpClient(_handler), _time, url => _openedUrl = url);
            service.StateChanged += () => _stateChangedCount++;
            return service;
        }

        [Fact]
        public async Task StartAuthorization_WithoutClientId_Throws()
        {
            var options = new YandexDiskOptions { ClientId = "" };
            var service = new YandexDiskAuthService(options, _store, new HttpClient(_handler), _time);

            var act = () => service.StartAuthorizationAsync();

            await act.Should().ThrowAsync<InvalidOperationException>();
            _openedUrl.Should().BeNull();
        }

        [Fact]
        public async Task StartAuthorization_OpensBrowserWithPkceUrl()
        {
            var service = CreateService();

            var start = await service.StartAuthorizationAsync();

            _openedUrl.Should().NotBeNull();
            var query = ParseQuery(new Uri(_openedUrl!).Query);

            query["response_type"].Should().Be("code");
            query["client_id"].Should().Be("test-client");
            query["redirect_uri"].Should().Be("https://oauth.yandex.ru/verification_code");
            query["scope"].Should().Be("cloud_api:disk.app_folder");
            query["code_challenge_method"].Should().Be("S256");
            query["code_challenge"].Should().NotBeNullOrEmpty();

            start.AuthorizationUrl.Should().Be(_openedUrl);
            start.VerifierExpiresAtUtc.Should().Be(StartTime.UtcDateTime + YandexDiskAuthService.PendingVerifierLifetime);
        }

        [Fact]
        public async Task CompleteAuthorization_ExchangesCodeWithoutSecret_AndStoresToken()
        {
            var service = CreateService();
            await service.StartAuthorizationAsync();

            _handler.Responses.Enqueue(CreateTokenResponse("tok-123", expiresIn: 3600));
            await service.CompleteAuthorizationAsync("  user-code-42  ");

            // Запрос обмена: grant_type=code, client_id, code_verifier — и НИЧЕГО про client_secret.
            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Method.Should().Be("POST");
            request.Url.Should().Be("https://oauth.yandex.ru/token");
            request.Body.Should().NotBeNull();

            var form = ParseForm(request.Body!);
            form["grant_type"].Should().Be("authorization_code");
            form["code"].Should().Be("user-code-42");
            form["client_id"].Should().Be("test-client");
            form.Should().ContainKey("code_verifier");
            form.Keys.Should().NotContain("client_secret");

            // PKCE: code_challenge в URL авторизации = S256(code_verifier), переданного при обмене.
            var challengeFromUrl = ParseQuery(new Uri(_openedUrl!).Query)["code_challenge"];
            ComputeS256Challenge(form["code_verifier"]).Should().Be(challengeFromUrl);

            var saved = await _store.GetAsync();
            saved.Should().NotBeNull();
            saved!.AccessToken.Should().Be("tok-123");
            saved.ExpiresAtUtc.Should().Be(StartTime.UtcDateTime + TimeSpan.FromHours(1));

            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.Connected);
            // Два уведомления: вход в ожидание кода (NotConnected → AwaitingCode) и подключение (→ Connected).
            _stateChangedCount.Should().Be(2);

            // Токен не попадает в лог запросов (тело ответа не записывается, заголовки не передавались).
            request.Body.Should().NotContain("tok-123");
        }

        [Fact]
        public async Task CompleteAuthorization_WithoutStart_Throws()
        {
            var service = CreateService();

            var act = () => service.CompleteAuthorizationAsync("some-code");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*активной попытки входа*");
        }

        [Fact]
        public async Task CompleteAuthorization_AfterVerifierExpiry_ThrowsAndAllowsReconnect()
        {
            var service = CreateService();
            await service.StartAuthorizationAsync();

            _time.Advance(YandexDiskAuthService.PendingVerifierLifetime + TimeSpan.FromSeconds(1));
            _handler.Responses.Enqueue(CreateTokenResponse("tok-late", 3600));

            var act = () => service.CompleteAuthorizationAsync("some-code");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Срок ввода кода истёк*");

            (await _store.GetAsync()).Should().BeNull();
            _handler.Requests.Should().BeEmpty();

            // Истёкшая попытка не блокирует UI: сервис вернулся в устойчивое состояние (YD-03).
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.NotConnected);
            await service.StartAuthorizationAsync();
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.AwaitingCode);
            _handler.Responses.Enqueue(CreateTokenResponse("tok-retry", 3600));
            await service.CompleteAuthorizationAsync("fresh-code");
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.Connected);
        }

        [Fact]
        public async Task StartAuthorization_Twice_ReplacesPendingVerifier()
        {
            var service = CreateService();
            await service.StartAuthorizationAsync();
            _time.Advance(TimeSpan.FromMinutes(9));
            await service.StartAuthorizationAsync(); // вторая попытка заменяет устаревающий verifier

            _time.Advance(TimeSpan.FromMinutes(9)); // 18 минут от первой попытки, 9 от второй
            _handler.Responses.Enqueue(CreateTokenResponse("tok-second", 3600));

            var act = () => service.CompleteAuthorizationAsync("code-second");

            await act.Should().NotThrowAsync();
            (await _store.GetAsync())!.AccessToken.Should().Be("tok-second");
        }

        [Fact]
        public async Task StartAuthorization_WhenBrowserFails_ReturnsUrlAndStaysAwaiting()
        {
            // YD-10 (долгосрочное): неоткрывшийся браузер не отменяет вход —
            // URL действителен и может быть открыт вручную.
            var service = new YandexDiskAuthService(_options, _store, new HttpClient(_handler), _time, _ => throw new InvalidOperationException("браузер запрещён политикой"));

            var start = await service.StartAuthorizationAsync();

            start.BrowserOpened.Should().BeFalse();
            start.AuthorizationUrl.Should().Contain("oauth.yandex.ru/authorize");
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.AwaitingCode);

            // Вход остаётся рабочим: код по ссылке обменивается на токен.
            _handler.Responses.Enqueue(CreateTokenResponse("tok-manual", 3600));
            await service.CompleteAuthorizationAsync("manual-code");
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.Connected);
        }

        [Fact]
        public void IsClientIdConfigured_ReflectsOptionsValue()
        {
            var options = new YandexDiskOptions { ClientId = string.Empty };
            var service = new YandexDiskAuthService(options, _store, new HttpClient(_handler), _time);

            service.IsClientIdConfigured().Should().BeFalse();

            options.ClientId = "client-id-abc";
            service.IsClientIdConfigured().Should().BeTrue("значение, введённое пользователем, обновляет живой экземпляр опций");
        }

        [Fact]
        public async Task GetValidAccessToken_ReturnsNullAndInvalidates_WhenTokenExpired()
        {
            var service = CreateService();
            await service.StartAuthorizationAsync();
            _handler.Responses.Enqueue(CreateTokenResponse("tok-short", expiresIn: 600)); // 10 минут
            await service.CompleteAuthorizationAsync("code");

            _time.Advance(TimeSpan.FromMinutes(8)); // срок 10 минут, сервис обновляет на 1 минуту раньше
            (await service.GetValidAccessTokenAsync()).Should().NotBeNull();

            _time.Advance(TimeSpan.FromMinutes(3)); // 11 минут — токен истёк
            (await service.GetValidAccessTokenAsync()).Should().BeNull();

            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.AuthRequired);
            (await _store.GetAsync()).Should().BeNull();
        }

        [Fact]
        public async Task CompleteAuthorization_WhenYandexReturnsError_ThrowsAndKeepsState()
        {
            var service = CreateService();
            await service.StartAuthorizationAsync();
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"code expired\"}")
            });

            var act = () => service.CompleteAuthorizationAsync("bad-code");

            await act.Should().ThrowAsync<InvalidOperationException>();
            (await _store.GetAsync()).Should().BeNull();
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.NotConnected);

            // Отклонённый код не оставляет зависшего ожидания: повторное подключение возможно сразу (YD-03).
            (await service.GetStateAsync()).Should().NotBe(YandexDiskAuthState.AwaitingCode);
            await service.StartAuthorizationAsync();
            _handler.Responses.Enqueue(CreateTokenResponse("tok-retry", 3600));
            await service.CompleteAuthorizationAsync("good-code");
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.Connected);
        }

        [Fact]
        public async Task CompleteAuthorization_WhenNetworkFails_ReturnsToStoredStateAndAllowsReconnect()
        {
            var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("сеть недоступна"));
            var service = new YandexDiskAuthService(_options, _store, new HttpClient(handler), _time, url => _openedUrl = url);
            await service.StartAuthorizationAsync();

            var act = () => service.CompleteAuthorizationAsync("some-code");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Проверьте подключение к интернету*");
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.NotConnected);

            await service.StartAuthorizationAsync();
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.AwaitingCode);
        }

        [Fact]
        public async Task CancelAuthorization_ClearsPending_AndRestoresNotConnected()
        {
            var service = CreateService();
            await service.StartAuthorizationAsync();
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.AwaitingCode);

            await service.CancelAuthorizationAsync();

            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.NotConnected);
            var act = () => service.CompleteAuthorizationAsync("late-code");
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*активной попытки входа*");
            _handler.Requests.Should().BeEmpty("обмен после отмены не выполняется");
        }

        [Fact]
        public async Task CancelAuthorization_WithExpiredTokenInStore_RestoresAuthRequired()
        {
            // Истёкший токен на диске: устойчивое состояние AuthRequired, а после отмены входа к нему и возвращаемся.
            await _store.SaveAsync(new YandexDiskToken("old-tok", StartTime.UtcDateTime - TimeSpan.FromHours(1)));
            var service = CreateService();
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.AuthRequired);

            await service.StartAuthorizationAsync();
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.AwaitingCode);

            await service.CancelAuthorizationAsync();
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.AuthRequired);
        }

        [Fact]
        public async Task Disconnect_RemovesToken_AndResetsState()
        {
            var service = CreateService();
            await service.StartAuthorizationAsync();
            _handler.Responses.Enqueue(CreateTokenResponse("tok", 3600));
            await service.CompleteAuthorizationAsync("code");

            await service.DisconnectAsync();

            (await _store.GetAsync()).Should().BeNull();
            (await service.GetStateAsync()).Should().Be(YandexDiskAuthState.NotConnected);
            (await service.GetValidAccessTokenAsync()).Should().BeNull();
        }

        private static HttpResponseMessage CreateTokenResponse(string accessToken, long expiresIn)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { access_token = accessToken, token_type = "bearer", expires_in = expiresIn }))
            };

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>();
            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                result[parts[0]] = Uri.UnescapeDataString(parts[1]);
            }
            return result;
        }

        private static Dictionary<string, string> ParseForm(string body)
        {
            var result = new Dictionary<string, string>();
            foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                result[parts[0]] = Uri.UnescapeDataString(parts[1].Replace('+', ' '));
            }
            return result;
        }

        private static string ComputeS256Challenge(string verifier)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
            return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
