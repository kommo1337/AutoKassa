using Serilog;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Реализация OAuth-авторизации Яндекс Диска.
    /// Запрос токена выполняется без client_secret (поток с вводом кода вручную);
    /// PKCE S256 подтверждает, что код обменивает то же приложение, которое начало вход.
    /// Токен, одноразовый код и code_verifier никогда не логируются и не покидают сервис.
    /// </summary>
    public sealed class YandexDiskAuthService : IYandexDiskAuthService
    {
        private static readonly ILogger _log = Log.ForContext<YandexDiskAuthService>();

        // code_verifier — 64 случайных байта в base64url (86 символов, допустимо 43–128 по RFC 7636).
        private const int CodeVerifierBytes = 64;

        // Незавершённая попытка входа живёт не дольше 10 минут и заменяется при повторной попытке.
        public static readonly TimeSpan PendingVerifierLifetime = TimeSpan.FromMinutes(10);

        // Действующий токен обновляется заранее, чтобы не поймать 401 посреди выгрузки.
        private static readonly TimeSpan TokenExpirySkew = TimeSpan.FromMinutes(1);

        // Яндекс не всегда возвращает expires_in — считаем токен годным на час.
        private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromHours(1);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = false
        };

        private readonly YandexDiskOptions _options;
        private readonly IYandexDiskTokenStore _tokenStore;
        private readonly HttpClient _httpClient;
        private readonly TimeProvider _time;
        private readonly Action<string> _openBrowser;

        private readonly object _sync = new();
        private YandexDiskAuthState _state = YandexDiskAuthState.NotConnected;
        private YandexDiskToken? _loadedToken;
        private bool _stateLoaded;
        private long _connectionGeneration;
        private PendingAttempt? _pending;

        public YandexDiskAuthService(
            YandexDiskOptions options,
            IYandexDiskTokenStore tokenStore,
            HttpClient httpClient,
            TimeProvider? time = null,
            Action<string>? openBrowser = null)
        {
            _options = options;
            _tokenStore = tokenStore;
            _httpClient = httpClient;
            _time = time ?? TimeProvider.System;
            _openBrowser = openBrowser ?? OpenBrowserDefault;
        }

        /// <inheritdoc />
        public event Action? StateChanged;

        /// <inheritdoc />
        public bool IsClientIdConfigured()
            => !string.IsNullOrWhiteSpace(_options.ClientId);

        /// <inheritdoc />
        public long ConnectionGeneration
        {
            get
            {
                lock (_sync)
                {
                    return _connectionGeneration;
                }
            }
        }

        /// <inheritdoc />
        public async Task<YandexDiskAuthState> GetStateAsync(CancellationToken ct = default)
        {
            await EnsureStateLoadedAsync(ct).ConfigureAwait(false);
            lock (_sync)
            {
                return _state;
            }
        }

        /// <inheritdoc />
        public async Task<YandexDiskAuthStart> StartAuthorizationAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ClientId))
                throw new InvalidOperationException("Яндекс Диск не настроен: отсутствует ClientId приложения. Укажите его в диалоге подключения.");

            var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(CodeVerifierBytes));
            var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var expiresAtUtc = _time.GetUtcNow().UtcDateTime + PendingVerifierLifetime;

            lock (_sync)
            {
                _pending = new PendingAttempt(verifier, expiresAtUtc);
            }

            var url = string.Concat(
                _options.AuthorizeUrl,
                "?response_type=code",
                "&client_id=", Uri.EscapeDataString(_options.ClientId),
                "&redirect_uri=", Uri.EscapeDataString(_options.RedirectUri),
                "&scope=", Uri.EscapeDataString(_options.Scope),
                "&code_challenge=", challenge,
                "&code_challenge_method=S256");

            var browserOpened = true;
            try
            {
                _openBrowser(url);
                _log.Information("Открыт браузер для авторизации Яндекс Диска (scope={Scope})", _options.Scope);
            }
            catch (Exception ex)
            {
                // Браузер не открылся (политика ОС, нестандартный браузер) — это не причина
                // отменять вход: URL возвращается вызывающему и может быть открыт вручную.
                // Сам URL не логируем: в нём code_challenge и служебные параметры (TRAPS §3.16).
                browserOpened = false;
                _log.Error(ex, "Не удалось автоматически открыть браузер для авторизации Яндекс Диска — URL доступен для ручного открытия");
            }
            SetState(YandexDiskAuthState.AwaitingCode);

            return new YandexDiskAuthStart(url, expiresAtUtc, browserOpened);
        }

        /// <inheritdoc />
        public async Task CompleteAuthorizationAsync(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Введите одноразовый код, показанный в браузере после разрешения доступа.");

            PendingAttempt pending;
            bool expired;
            lock (_sync)
            {
                if (_pending == null)
                    throw new InvalidOperationException("Сначала нажмите «Подключить» — активной попытки входа нет.");
                expired = _pending.ExpiresAtUtc <= _time.GetUtcNow().UtcDateTime;
                pending = _pending;
                _pending = null;
            }

            if (expired)
            {
                // Попытка не вернётся — возвращаемся в устойчивое состояние,
                // чтобы UI снова разрешил «Подключить» без перезапуска приложения.
                await ResetToStoredStateAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException("Срок ввода кода истёк. Нажмите «Подключить» ещё раз.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code.Trim(),
                ["client_id"] = _options.ClientId,
                ["code_verifier"] = pending.Verifier
                // client_secret намеренно не передаётся: общий секрет нельзя встраивать в WPF-приложение.
            });

            try
            {
                using var httpResponse = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                var body = await httpResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var response = JsonSerializer.Deserialize<OAuthTokenResponse>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Яндекс вернул пустой ответ при обмене кода на токен.");

                if (!string.IsNullOrEmpty(response.Error))
                {
                    // Логируем только поле error — без error_description и тела ответа (см. TRAPS §3.16).
                    _log.Warning("Яндекс отклонил обмен кода на токен: {Error}", response.Error);
                    throw new InvalidOperationException("Яндекс Диск не подключён: доступ отклонён или код введён неверно. Попробуйте ещё раз.");
                }

                if (string.IsNullOrEmpty(response.AccessToken))
                    throw new InvalidOperationException("Яндекс Диск не подключён: в ответе отсутствует токен доступа.");

                var lifetime = response.ExpiresIn > 0
                    ? TimeSpan.FromSeconds(response.ExpiresIn)
                    : DefaultTokenLifetime;
                var token = new YandexDiskToken(response.AccessToken, _time.GetUtcNow().UtcDateTime + lifetime);

                await _tokenStore.SaveAsync(token, ct).ConfigureAwait(false);
                lock (_sync)
                {
                    _loadedToken = token;
                    // Новый токен = новое поколение: старые операции не должны его инвалидировать.
                    _connectionGeneration++;
                }
                SetState(YandexDiskAuthState.Connected);
                _log.Information("Яндекс Диск подключён, токен получен (срок действия до {ExpiresAtUtc})", token.ExpiresAtUtc);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException)
            {
                // Не включаем тело ответа в сообщение — оно может содержать код или служебные данные.
                _log.Error(ex, "Сетевая ошибка при обмене кода на токен Яндекс Диска");
                await ResetToStoredStateAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException("Не удалось подключить Яндекс Диск. Проверьте подключение к интернету и попробуйте ещё раз.");
            }
            catch
            {
                // Бизнес-ошибки обмена (invalid_grant и т.п.) — тоже возвращаем в устойчивое
                // состояние: подключение можно повторить сразу, а не после перезапуска.
                await ResetToStoredStateAsync(ct).ConfigureAwait(false);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CancelAuthorizationAsync(CancellationToken ct = default)
        {
            lock (_sync)
            {
                _pending = null;
            }
            await ResetToStoredStateAsync(ct).ConfigureAwait(false);
            _log.Information("Ввод кода авторизации Яндекс Диска отменён пользователем");
        }

        /// <inheritdoc />
        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            lock (_sync)
            {
                _pending = null;
                _connectionGeneration++;
            }

            await _tokenStore.ClearAsync(ct).ConfigureAwait(false);
            SetState(YandexDiskAuthState.NotConnected);
            _log.Information("Яндекс Диск отключён");
        }

        /// <inheritdoc />
        public async Task<YandexDiskToken?> GetValidAccessTokenAsync(CancellationToken ct = default)
        {
            await EnsureStateLoadedAsync(ct).ConfigureAwait(false);

            YandexDiskToken? token;
            lock (_sync)
            {
                token = _state == YandexDiskAuthState.Connected ? _loadedToken : null;
            }

            if (token == null)
                return null;

            if (token.ExpiresAtUtc - TokenExpirySkew <= _time.GetUtcNow().UtcDateTime)
            {
                // Refresh grant недоступен без client_secret — истёкший токен = повторный вход.
                await InvalidateTokenAsync(ct).ConfigureAwait(false);
                return null;
            }

            return token;
        }

        /// <inheritdoc />
        public async Task InvalidateTokenAsync(CancellationToken ct = default)
        {
            lock (_sync)
            {
                _connectionGeneration++;
            }

            await _tokenStore.ClearAsync(ct).ConfigureAwait(false);
            SetState(YandexDiskAuthState.AuthRequired);
            _log.Information("Токен Яндекс Диска инвалидирован — требуется повторный вход");
        }

        private async Task EnsureStateLoadedAsync(CancellationToken ct)
        {
            lock (_sync)
            {
                if (_stateLoaded)
                    return;
            }

            var token = await _tokenStore.GetAsync(ct).ConfigureAwait(false);

            lock (_sync)
            {
                if (_stateLoaded)
                    return;
                _loadedToken = token;
                // Транзитное AwaitingCode не перезаписываем выводом из хранилища —
                // ожидание кода существует только в памяти и не привязано к токену.
                if (_state != YandexDiskAuthState.AwaitingCode)
                {
                    _state = token == null
                        ? YandexDiskAuthState.NotConnected
                        : token.ExpiresAtUtc - TokenExpirySkew > _time.GetUtcNow().UtcDateTime
                            ? YandexDiskAuthState.Connected
                            : YandexDiskAuthState.AuthRequired;
                }
                _stateLoaded = true;
            }
        }

        /// <summary>
        /// Вернуть устойчивое состояние из хранилища токена, завершив транзитное AwaitingCode.
        /// Поднимает StateChanged только при реальном изменении состояния.
        /// </summary>
        private async Task ResetToStoredStateAsync(CancellationToken ct)
        {
            YandexDiskAuthState previous;
            lock (_sync)
            {
                previous = _state;
                // Нейтральная заглушка: EnsureStateLoadedAsync не сохранит AwaitingCode и пересчитает из хранилища.
                _state = YandexDiskAuthState.NotConnected;
                _stateLoaded = false;
            }

            await EnsureStateLoadedAsync(ct).ConfigureAwait(false);

            YandexDiskAuthState current;
            lock (_sync)
            {
                current = _state;
            }
            if (current != previous)
                StateChanged?.Invoke();
        }

        private void SetState(YandexDiskAuthState state)
        {
            lock (_sync)
            {
                if (_state == state)
                    return;
                _state = state;
                if (state != YandexDiskAuthState.Connected)
                    _loadedToken = null;
            }

            StateChanged?.Invoke();
        }

        private static void OpenBrowserDefault(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Не удалось открыть браузер для авторизации Яндекс Диска");
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private sealed record PendingAttempt(string Verifier, DateTime ExpiresAtUtc);
    }
}
