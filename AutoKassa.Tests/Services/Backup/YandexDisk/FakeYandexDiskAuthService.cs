using AutoKassa.Services.Backup.YandexDisk;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>
    /// Фейк IYandexDiskAuthService для тестов: управляемое состояние подключения
    /// и перехватчики для сценариев ошибок. Браузер не открывает.
    /// </summary>
    public sealed class FakeYandexDiskAuthService : IYandexDiskAuthService
    {
        public YandexDiskAuthState State { get; set; } = YandexDiskAuthState.NotConnected;

        /// <summary>Значение BrowserOpened для результата StartAuthorizationAsync (YD-10).</summary>
        public bool BrowserOpened { get; set; } = true;

        /// <summary>Результат IsClientIdConfigured; false — эмуляция отсутствия ClientId.</summary>
        public bool Configured { get; set; } = true;

        public bool IsClientIdConfigured() => Configured;

        /// <summary>Выбрасывается из StartAuthorizationAsync, если задано.</summary>
        public Exception? StartException { get; set; }

        /// <summary>Выбрасывается из CompleteAuthorizationAsync, если задано.</summary>
        public Exception? CompleteException { get; set; }

        public int StartCallCount { get; private set; }
        public int CompleteCallCount { get; private set; }
        public int CancelCallCount { get; private set; }
        public int DisconnectCallCount { get; private set; }
        public string? LastCompletedCode { get; private set; }

        public event Action? StateChanged;

        public Task<YandexDiskAuthState> GetStateAsync(CancellationToken ct = default)
            => Task.FromResult(State);

        public Task<YandexDiskAuthStart> StartAuthorizationAsync(CancellationToken ct = default)
        {
            StartCallCount++;
            if (StartException != null)
                throw StartException;

            State = YandexDiskAuthState.AwaitingCode;
            StateChanged?.Invoke();
            return Task.FromResult(new YandexDiskAuthStart(
                "https://oauth.yandex.ru/authorize?test=1",
                DateTime.UtcNow.AddMinutes(10),
                BrowserOpened));
        }

        public Task CompleteAuthorizationAsync(string code, CancellationToken ct = default)
        {
            CompleteCallCount++;
            LastCompletedCode = code;
            if (CompleteException != null)
            {
                // Как и реальный сервис: неуспешный обмен завершает ожидание кода.
                State = YandexDiskAuthState.NotConnected;
                StateChanged?.Invoke();
                throw CompleteException;
            }

            State = YandexDiskAuthState.Connected;
            StateChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task CancelAuthorizationAsync(CancellationToken ct = default)
        {
            CancelCallCount++;
            State = YandexDiskAuthState.NotConnected;
            StateChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            DisconnectCallCount++;
            State = YandexDiskAuthState.NotConnected;
            StateChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task<YandexDiskToken?> GetValidAccessTokenAsync(CancellationToken ct = default)
            => Task.FromResult<YandexDiskToken?>(null);

        public Task InvalidateTokenAsync(CancellationToken ct = default)
        {
            State = YandexDiskAuthState.AuthRequired;
            StateChanged?.Invoke();
            return Task.CompletedTask;
        }

        public void RaiseStateChanged() => StateChanged?.Invoke();
    }
}
