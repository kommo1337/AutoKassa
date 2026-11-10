using AutoKassa.Services.Backup.YandexDisk;
using FluentAssertions;
using System.Net;
using Xunit;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    public class YandexDiskApiClientTests : IDisposable
    {
        private readonly FakeHttpMessageHandler _handler;
        private readonly YandexDiskApiClient _client;
        private readonly string _tempFile;

        public YandexDiskApiClientTests()
        {
            _handler = new FakeHttpMessageHandler();
            var options = new YandexDiskOptions { ClientId = "test", ApiBaseUrl = "https://cloud-api.test" };
            _client = new YandexDiskApiClient(new HttpClient(_handler), options, new StubAuthService());
            _tempFile = Path.GetTempFileName();
        }

        public void Dispose()
        {
            try { File.Delete(_tempFile); } catch { }
        }

        [Fact]
        public async Task GetUploadLink_BuildsUrlAndSendsOAuthHeader()
        {
            _handler.Responses.Enqueue(JsonResponse("{\"href\":\"https://upload.test/abc\",\"method\":\"PUT\"}"));

            var link = await _client.GetUploadLinkAsync("app:/AutoKassa_Backup_20260101_120000.db", overwrite: false);

            link.Href.Should().Be("https://upload.test/abc");
            link.Method.Should().Be("PUT");

            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Method.Should().Be("GET");
            request.Url.Should().Contain("https://cloud-api.test/v1/disk/resources/upload?");
            request.Url.Should().Contain("path=app%3A%2FAutoKassa_Backup_20260101_120000.db");
            request.Url.Should().Contain("overwrite=false");
            request.Authorization.Should().Be("OAuth test-token");
        }

        [Fact]
        public async Task UploadFile_ReturnsCreatedOn201()
        {
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));

            var result = await _client.UploadFileAsync("https://upload.test/abc", _tempFile);

            result.Should().Be(YandexDiskUploadResult.Created);
            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Method.Should().Be("PUT");
            request.Url.Should().Be("https://upload.test/abc");
            request.Authorization.Should().BeNull(); // загрузка по выданной ссылке, OAuth-заголовок не нужен
        }

        [Fact]
        public async Task UploadFile_ReturnsAcceptedOn202()
        {
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Accepted));

            var result = await _client.UploadFileAsync("https://upload.test/abc", _tempFile);

            result.Should().Be(YandexDiskUploadResult.Accepted);
        }

        [Fact]
        public async Task GetResource_ParsesMetadata()
        {
            _handler.Responses.Enqueue(JsonResponse(
                "{\"name\":\"AutoKassa_Backup_20260101_120000.db\",\"size\":12345,\"md5\":\"abcdef0123456789\",\"created\":\"2026-01-01T12:00:05Z\",\"type\":\"file\"}"));

            var info = await _client.GetResourceAsync("app:/AutoKassa_Backup_20260101_120000.db");

            info.Name.Should().Be("AutoKassa_Backup_20260101_120000.db");
            info.Size.Should().Be(12345);
            info.Md5.Should().Be("abcdef0123456789");
            info.CreatedUtc.Should().Be(new DateTime(2026, 1, 1, 12, 0, 5, DateTimeKind.Utc));
            info.Type.Should().Be("file");
        }

        [Fact]
        public async Task GetAppFolderResources_ReadsAllPages()
        {
            // Реальный формат ответа API: список лежит во вложенном _embedded, поле total — общее число ресурсов.
            // Страница 1 — полная (100 элементов → клиент запрашивает следующую), страница 2 — частичная.
            var firstPage = System.Text.Json.JsonSerializer.Serialize(new
            {
                _embedded = new
                {
                    items = Enumerable.Range(0, 100).Select(i => new { name = $"f{i}.db", size = i, md5 = (string?)null, type = "file" }),
                    total = 101
                }
            });
            var secondPage = System.Text.Json.JsonSerializer.Serialize(new
            {
                _embedded = new
                {
                    items = new[] { new { name = "f100.db", size = 100, md5 = (string?)null, type = "file" } },
                    total = 101
                }
            });
            _handler.Responses.Enqueue(JsonResponse(firstPage));
            _handler.Responses.Enqueue(JsonResponse(secondPage));

            var resources = await _client.GetAppFolderResourcesAsync();

            resources.Should().HaveCount(101);
            _handler.Requests.Should().HaveCount(2);
            _handler.Requests[0].Url.Should().Contain("offset=0");
            _handler.Requests[1].Url.Should().Contain("offset=100");
            _handler.Requests.Should().OnlyContain(r => r.Url.Contains("path=app%3A%2F"));
            _handler.Requests.Should().OnlyContain(r => r.Url.Contains("fields="));
        }

        [Fact]
        public async Task GetAppFolderResources_StopsWhenTotalReachedOnFullPage()
        {
            // Полная страница, но total уже достигнут — следующую страницу запрашивать не нужно.
            var page = System.Text.Json.JsonSerializer.Serialize(new
            {
                _embedded = new
                {
                    items = Enumerable.Range(0, 100).Select(i => new { name = $"f{i}.db", size = i, md5 = (string?)null, type = "file" }),
                    total = 100
                }
            });
            _handler.Responses.Enqueue(JsonResponse(page));

            var resources = await _client.GetAppFolderResourcesAsync();

            resources.Should().HaveCount(100);
            _handler.Requests.Should().ContainSingle();
        }

        [Fact]
        public async Task GetAppFolderResources_EmptyFolder_ReturnsEmpty()
        {
            _handler.Responses.Enqueue(JsonResponse("{\"_embedded\":{\"items\":[],\"total\":0}}"));

            var resources = await _client.GetAppFolderResourcesAsync();

            resources.Should().BeEmpty();
            _handler.Requests.Should().ContainSingle();
        }

        [Fact]
        public async Task GetAppFolderResources_WithoutEmbedded_ReturnsEmpty()
        {
            // Ответ без _embedded (например, ресурс без содержимого) — не ошибка, просто пустой список.
            _handler.Responses.Enqueue(JsonResponse("{\"name\":\"app:\",\"type\":\"dir\"}"));

            var resources = await _client.GetAppFolderResourcesAsync();

            resources.Should().BeEmpty();
        }

        [Fact]
        public async Task EnsureSuccess_MapsStatusCodesToTypedExceptions()
        {
            // 401 → Auth
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            var act401 = () => _client.GetResourceAsync("app:/x.db");
            await act401.Should().ThrowAsync<YandexDiskAuthException>();

            // 409 → Conflict
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Conflict));
            var act409 = () => _client.GetUploadLinkAsync("app:/x.db", overwrite: false);
            await act409.Should().ThrowAsync<YandexDiskConflictException>();

            // 403 → Quota
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
            var act403 = () => _client.GetResourceAsync("app:/x.db");
            await act403.Should().ThrowAsync<YandexDiskQuotaException>();

            // 507 → Quota
            _handler.Responses.Enqueue(new HttpResponseMessage((HttpStatusCode)507));
            var act507 = () => _client.GetResourceAsync("app:/x.db");
            await act507.Should().ThrowAsync<YandexDiskQuotaException>();

            // 500 → Transient
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var act500 = () => _client.GetResourceAsync("app:/x.db");
            await act500.Should().ThrowAsync<YandexDiskTransientException>();

            // 429 → Transient с Retry-After
            var tooMany = new HttpResponseMessage((HttpStatusCode)429);
            tooMany.Headers.TryAddWithoutValidation("Retry-After", "30");
            _handler.Responses.Enqueue(tooMany);
            try
            {
                await _client.GetResourceAsync("app:/x.db");
                Assert.Fail("Ожидалось YandexDiskTransientException");
            }
            catch (YandexDiskTransientException ex)
            {
                ex.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
            }
        }

        [Fact]
        public async Task GetResource_WhenResourceMissing_ThrowsNotFound()
        {
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"DiskNotFoundError\",\"error_description\":\"not found\"}")
            });

            var act = () => _client.GetResourceAsync("app:/missing.db");

            // Типизированный 404: ожидание ресурса отличает «ещё не появился» от ошибок.
            await act.Should().ThrowAsync<YandexDiskNotFoundException>()
                .WithMessage("*404*");
        }

        [Fact]
        public async Task GetResource_WhenApiTimesOut_ThrowsTransientInsteadOfCancellation()
        {
            // Медленный ответ длиннее короткого ApiRequestTimeout; внешний токен не отменён.
            var handler = new FakeHttpMessageHandler(async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            });
            var options = new YandexDiskOptions
            {
                ClientId = "test",
                ApiBaseUrl = "https://cloud-api.test",
                ApiRequestTimeout = TimeSpan.FromMilliseconds(150)
            };
            var client = new YandexDiskApiClient(new HttpClient(handler), options, new StubAuthService());

            var act = () => client.GetResourceAsync("app:/x.db");

            await act.Should().ThrowAsync<YandexDiskTransientException>(
                "таймаут — повторяемый сбой, а не штатная отмена операции");
        }

        [Fact]
        public async Task UploadFile_WhenUploadTimesOut_ThrowsTransient()
        {
            var handler = new FakeHttpMessageHandler(async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return new HttpResponseMessage(HttpStatusCode.Created);
            });
            var options = new YandexDiskOptions
            {
                ClientId = "test",
                ApiBaseUrl = "https://cloud-api.test",
                UploadTimeout = TimeSpan.FromMilliseconds(150)
            };
            var client = new YandexDiskApiClient(new HttpClient(handler), options, new StubAuthService());

            var act = () => client.UploadFileAsync("https://upload.test/abc", _tempFile);

            await act.Should().ThrowAsync<YandexDiskTransientException>(
                "большая загрузка имеет отдельный щедрый лимит; его превышение повторяемо");
        }

        [Fact]
        public async Task GetResource_WhenCancelledExternally_PropagatesCancellation()
        {
            var handler = new FakeHttpMessageHandler(async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            });
            var options = new YandexDiskOptions
            {
                ClientId = "test",
                ApiBaseUrl = "https://cloud-api.test",
                ApiRequestTimeout = TimeSpan.FromMinutes(1)
            };
            var client = new YandexDiskApiClient(new HttpClient(handler), options, new StubAuthService());
            using var cts = new CancellationTokenSource(150);

            try
            {
                await client.GetResourceAsync("app:/x.db", cts.Token);
                Assert.Fail("Ожидалась штатная отмена");
            }
            catch (YandexDiskTransientException)
            {
                Assert.Fail("Штатная отмена по внешнему токену не должна превращаться в повторяемый сбой");
            }
            catch (OperationCanceledException)
            {
                // Штатная отмена — пробрасывается как есть.
            }
        }

        [Fact]
        public async Task DeleteResource_Accepts204And202()
        {
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
            _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Accepted));

            var act = async () =>
            {
                await _client.DeleteResourceAsync("app:/old1.db");
                await _client.DeleteResourceAsync("app:/old2.db");
            };

            await act.Should().NotThrowAsync();
            _handler.Requests.Should().OnlyContain(r => r.Method == "DELETE");
            _handler.Requests.Should().OnlyContain(r => r.Url.Contains("permanently=true"));
        }

        private static HttpResponseMessage JsonResponse(string json, HttpStatusCode code = HttpStatusCode.OK)
            => new(code) { Content = new StringContent(json) };

        /// <summary>Auth-сервис-заглушка: всегда выдаёт действительный токен.</summary>
        private sealed class StubAuthService : IYandexDiskAuthService
        {
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
            public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
            public Task<YandexDiskToken?> GetValidAccessTokenAsync(CancellationToken ct = default)
                => Task.FromResult<YandexDiskToken?>(new YandexDiskToken("test-token", DateTime.UtcNow.AddHours(1)));
            public Task InvalidateTokenAsync(CancellationToken ct = default) => Task.CompletedTask;
        }
    }
}
