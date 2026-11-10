namespace AutoKassa.Services.Backup.YandexDisk
{
    /// <summary>
    /// Состояние подключения аккаунта Яндекс Диска.
    /// </summary>
    public enum YandexDiskAuthState
    {
        /// <summary>Аккаунт не подключён (токена нет).</summary>
        NotConnected = 0,

        /// <summary>Токен действителен — облачные операции разрешены.</summary>
        Connected = 1,

        /// <summary>Токен истёк или отозван — требуется повторный вход через браузер.</summary>
        AuthRequired = 2,

        /// <summary>
        /// Транзитное состояние: браузер открыт, ожидается ввод одноразового кода.
        /// Живёт только в памяти процесса, в хранилище не сохраняется; любой неуспешный
        /// обмен кодом или отмена возвращают сервис в устойчивое состояние.
        /// </summary>
        AwaitingCode = 3
    }

    /// <summary>
    /// Результат начала авторизации: URL для открытия в браузере и срок жизни code_verifier.
    /// <see cref="BrowserOpened"/> = false, когда системный браузер не открылся:
    /// URL по-прежнему действителен — его можно открыть/скопировать вручную (YD-10).
    /// </summary>
    public sealed record YandexDiskAuthStart(string AuthorizationUrl, DateTime VerifierExpiresAtUtc, bool BrowserOpened = true);

    /// <summary>
    /// Авторизация в Яндекс ID через OAuth 2.0 + PKCE (S256) с системным браузером.
    /// Общий client_secret не используется и в приложение не встраивается;
    /// refresh-token не сохраняется — при истечении срока требуется повторный вход.
    /// </summary>
    public interface IYandexDiskAuthService
    {
        /// <summary>Текущее состояние подключения (ленивая загрузка из хранилища при первом вызове).</summary>
        Task<YandexDiskAuthState> GetStateAsync(CancellationToken ct = default);

        /// <summary>
        /// Задан ли действующий ClientId (appsettings.json или введённый пользователем
        /// через <see cref="IYandexDiskClientIdStore"/>). Проверка до старта авторизации:
        /// если false — UI предлагает диалог ввода ClientId.
        /// </summary>
        bool IsClientIdConfigured();

        /// <summary>Событие смены состояния подключения.</summary>
        event Action? StateChanged;

        /// <summary>
        /// Начать авторизацию: сгенерировать PKCE code_verifier/code_challenge,
        /// открыть системный браузер на странице разрешения доступа.
        /// Повторный вызов заменяет незавершённый code_verifier.
        /// </summary>
        /// <returns>URL авторизации и срок жизни code_verifier (10 минут).</returns>
        /// <exception cref="InvalidOperationException">ClientId не задан в appsettings.json.</exception>
        Task<YandexDiskAuthStart> StartAuthorizationAsync(CancellationToken ct = default);

        /// <summary>
        /// Завершить авторизацию: обменять одноразовый код на access-token и сохранить его.
        /// Любой неуспешный обмен (отказ Яндекса, сеть, истёкший код) переводит сервис
        /// из <see cref="YandexDiskAuthState.AwaitingCode"/> в устойчивое состояние —
        /// повторная попытка начинается с «Подключить» без перезапуска приложения.
        /// </summary>
        /// <param name="code">Одноразовый код, показанный пользователю после разрешения доступа.</param>
        /// <exception cref="InvalidOperationException">Нет активной попытки входа, истёк code_verifier или Яндекс вернул ошибку.</exception>
        Task CompleteAuthorizationAsync(string code, CancellationToken ct = default);

        /// <summary>
        /// Отменить ожидание кода: забыть незавершённую попытку входа и вернуться
        /// в устойчивое состояние (NotConnected/AuthRequired по факту токена).
        /// </summary>
        Task CancelAuthorizationAsync(CancellationToken ct = default);

        /// <summary>Отключить аккаунт: удалить сохранённый токен и запретить облачные запросы.</summary>
        Task DisconnectAsync(CancellationToken ct = default);

        /// <summary>
        /// Получить действительный access-token для сетевых вызовов.
        /// Возвращает null, если токена нет или он истёк (в т.ч. после InvalidateTokenAsync).
        /// </summary>
        Task<YandexDiskToken?> GetValidAccessTokenAsync(CancellationToken ct = default);

        /// <summary>
        /// Инвалидировать токен (HTTP 401 от API или отзыв доступа):
        /// удалить сохранённый токен и перейти в состояние <see cref="YandexDiskAuthState.AuthRequired"/>.
        /// Очередь незагруженной копии не затрагивается.
        /// </summary>
        Task InvalidateTokenAsync(CancellationToken ct = default);
    }
}
