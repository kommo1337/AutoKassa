using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Services.Backup;
using AutoKassa.Services.Backup.YandexDisk;
using AutoKassa.Services.CreditCards;
using AutoKassa.Services.Settings;
using AutoKassa.Tests.Infrastructure;
using AutoKassa.Tests.Services.Backup;
using AutoKassa.Tests.Services.Backup.YandexDisk;
using AutoKassa.ViewModels;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.ViewModels
{
    /// <summary>
    /// Тесты облачного региона настроек (Яндекс Диск): команды OAuth и загрузки,
    /// дружелюбные ошибки, состояния и их обновление через события сервисов.
    /// </summary>
    public class SettingsViewModelCloudTests
    {
        private readonly FakeSettingsService _settings;
        private readonly FakeDialogService _dialog;
        private readonly FakeToastNotificationService _toast;
        private readonly FakeYandexDiskAuthService _auth;
        private readonly FakeYandexDiskBackupService _backup;
        private readonly FakeAutomaticBackupService _coordinator;
        private readonly FakeClipboardService _clipboard;
        private readonly SettingsViewModel _vm;

        public SettingsViewModelCloudTests()
        {
            _settings = new FakeSettingsService();
            _settings.Settings.AutoLockTimeout = 10;
            _dialog = new FakeDialogService();
            _toast = new FakeToastNotificationService();
            _auth = new FakeYandexDiskAuthService();
            _backup = new FakeYandexDiskBackupService();
            _coordinator = new FakeAutomaticBackupService();
            _clipboard = new FakeClipboardService();

            _vm = new SettingsViewModel(
                _settings,
                _dialog,
                _toast,
                new StubCategoryService(),
                new FakeDataChangeService(),
                new SettingsMapper(),
                new SettingsValidator(),
                new StubBackupCoordinator(),
                new StubAppInfoService(),
                new StubCreditCardSettingsService(),
                new FakeLockService(),
                _auth,
                _backup,
                _coordinator,
                _clipboard);
        }

        private async Task LoadAsync()
        {
            _vm.OnNavigatedTo();
            // AppVersion заполняется в конце успешной загрузки — надёжный признак завершения.
            await TestWaitHelper.WaitUntilAsync(() => _vm.AppVersion != null);
        }

        [Fact]
        public async Task Load_WhenConnected_PopulatesCloudStateFromServices()
        {
            _auth.State = YandexDiskAuthState.Connected;
            var lastSuccess = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
            _settings.Settings.YandexDiskLastBackupUtc = lastSuccess;
            _backup.CurrentState = new CloudBackupState(
                CloudBackupStatus.UpToDate, lastSuccess, false, "Облачная копия актуальна.");

            await LoadAsync();

            _vm.YandexAuthState.Should().Be(YandexDiskAuthState.Connected);
            _vm.CloudStatusMessage.Should().Be("Облачная копия актуальна.");
            _vm.CloudLastSuccessUtc.Should().Be(lastSuccess);
        }

        [Fact]
        public async Task ConnectCommand_WithoutClientId_ShowsEntryDialog()
        {
            // ClientId не задан — вместо предупреждения открывается диалог ввода.
            await LoadAsync();
            _auth.Configured = false;
            _dialog.DialogResult = false; // пользователь закрыл диалог

            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialog.LastDialogViewModelType != null);

            _dialog.LastDialogViewModelType.Should().Be(typeof(YandexClientIdDialogViewModel));
            _auth.StartCallCount.Should().Be(0, "подключение не начинается без ClientId");
        }

        [Fact]
        public async Task ConnectCommand_WithoutClientId_AfterEnteringValue_StartsAuthorization()
        {
            // Пользователь ввёл ClientId в диалоге — подключение продолжается без перезапуска.
            await LoadAsync();
            _auth.Configured = false;
            _dialog.DialogResult = true;
            _dialog.OnShowDialog = () => _auth.Configured = true;

            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudAwaitingCode);

            _auth.StartCallCount.Should().Be(1);
            _dialog.LastWarning.Should().BeNull();
            _vm.CloudAwaitingCode.Should().BeTrue();
        }

        [Fact]
        public async Task ConnectCommand_WithoutClientId_WhenValueNotSaved_ShowsWarningAndStops()
        {
            // Диалог подтверждён, но ClientId не появился (сбой сохранения) — честное предупреждение.
            await LoadAsync();
            _auth.Configured = false;
            _dialog.DialogResult = true;

            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialog.LastWarning != null);

            _dialog.LastWarning.Should().Contain("ClientId не сохранён");
            _auth.StartCallCount.Should().Be(0);
        }

        [Fact]
        public async Task ConnectCommand_StartsAuthorizationAndShowsCodeEntry()
        {
            await LoadAsync();

            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudAwaitingCode);

            _auth.StartCallCount.Should().Be(1);
            _dialog.LastError.Should().BeNull();
        }

        [Fact]
        public async Task ConnectCommand_WhenServiceThrowsBusinessError_ShowsServiceMessage()
        {
            await LoadAsync();
            _auth.StartException = new InvalidOperationException("Яндекс Диск не настроен: отсутствует ClientId.");

            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialog.LastWarning != null);

            _vm.CloudAwaitingCode.Should().BeFalse();
            _dialog.LastWarning.Should().Contain("ClientId");
        }

        [Fact]
        public async Task ConnectCommand_WhenUnexpectedError_ShowsFriendlyMessageWithoutDetails()
        {
            await LoadAsync();
            _auth.StartException = new Exception("stack trace details");

            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialog.LastError != null);

            _dialog.LastError.Should().NotContain("stack trace details");
            _dialog.LastError.Should().Contain("Не удалось подключить Яндекс Диск");
        }

        [Fact]
        public async Task ConfirmCodeCommand_CompletesAuthResetsDateAndWakesCoordinator()
        {
            await LoadAsync();
            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudAwaitingCode);
            _settings.Settings.YandexDiskLastBackupUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

            _vm.CloudAuthCode = "123456";
            _vm.ConfirmYandexCodeCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _auth.CompleteCallCount == 1);

            _auth.LastCompletedCode.Should().Be("123456");
            _vm.YandexAuthState.Should().Be(YandexDiskAuthState.Connected);
            _vm.CloudAwaitingCode.Should().BeFalse();
            _settings.Settings.YandexDiskLastBackupUtc.Should().BeNull(
                "при новом подключении дата сбрасывается — новая учётная запись сразу получит актуальную копию");
            _coordinator.RequestCheckCallCount.Should().BeGreaterThanOrEqualTo(1);
            _toast.LastSuccess.Should().Contain("подключён");
        }

        [Fact]
        public async Task ConfirmCodeCommand_WhenCodeRejected_ShowsServiceMessage()
        {
            await LoadAsync();
            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudAwaitingCode);
            _auth.CompleteException = new InvalidOperationException("Срок ввода кода истёк. Нажмите «Подключить» ещё раз.");

            _vm.CloudAuthCode = "000000";
            _vm.ConfirmYandexCodeCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialog.LastWarning != null);

            _dialog.LastWarning.Should().Contain("Срок ввода кода истёк");
        }

        [Fact]
        public async Task ConfirmCodeCommand_WhenCodeRejected_AllowsImmediateReconnect()
        {
            // YD-03: после неуспешного обмена ожидание кода сбрасывается —
            // «Подключить» доступно снова без перезапуска приложения.
            await LoadAsync();
            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudAwaitingCode);
            _auth.CompleteException = new InvalidOperationException("Яндекс Диск не подключён: доступ отклонён или код введён неверно. Попробуйте ещё раз.");

            _vm.CloudAuthCode = "000000";
            _vm.ConfirmYandexCodeCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialog.LastWarning != null);

            _vm.CloudAwaitingCode.Should().BeFalse("неуспешный обмен сбрасывает ожидание кода");
            _vm.CloudAuthCode.Should().BeEmpty();
            _vm.YandexAuthState.Should().Be(YandexDiskAuthState.NotConnected);
            _vm.ConnectYandexCommand.CanExecute(null).Should().BeTrue("повторное подключение доступно сразу");
        }

        [Fact]
        public async Task CancelYandexInputCommand_CancelsPendingAttemptAndClearsInput()
        {
            // YD-03 (долгосрочное): явная команда отмены ожидания кода.
            await LoadAsync();
            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudAwaitingCode);

            _vm.CloudAuthCode = "123456";
            _vm.CancelYandexInputCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _auth.CancelCallCount == 1);

            _auth.CancelCallCount.Should().Be(1);
            _vm.CloudAwaitingCode.Should().BeFalse();
            _vm.CloudAuthCode.Should().BeEmpty();
            _vm.YandexAuthState.Should().Be(YandexDiskAuthState.NotConnected);
            _vm.ConnectYandexCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public async Task ConnectCommand_WhenBrowserFails_ShowsWarningAndKeepsLinkAvailable()
        {
            // YD-10 (долгосрочное): браузер не открылся — дружелюбное предупреждение,
            // но ожидание кода продолжается: ссылку можно скопировать и открыть вручную.
            await LoadAsync();
            _auth.BrowserOpened = false;

            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudAwaitingCode);

            _dialog.LastWarning.Should().Contain("Скопируйте ссылку");
            _vm.CloudAwaitingCode.Should().BeTrue("вход остаётся активным — URL действителен");
            _vm.CloudAuthUrl.Should().Contain("oauth.yandex.ru/authorize");
            _vm.CopyYandexAuthLinkCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public async Task CopyYandexAuthLinkCommand_CopiesUrlAndShowsToast()
        {
            await LoadAsync();
            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudAwaitingCode);

            _vm.CopyYandexAuthLinkCommand.Execute(null);

            _clipboard.SetTextCallCount.Should().Be(1);
            _clipboard.LastText.Should().Be(_vm.CloudAuthUrl);
            _toast.LastSuccess.Should().Contain("скопирована");
        }

        [Fact]
        public async Task CopyYandexAuthLinkCommand_WhenClipboardBusy_ShowsError()
        {
            await LoadAsync();
            _clipboard.Succeeds = false;
            _vm.ConnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudAwaitingCode);

            _vm.CopyYandexAuthLinkCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialog.LastError != null);

            _dialog.LastError.Should().Contain("Не удалось скопировать ссылку");
        }

        [Fact]
        public async Task Load_RaisesNotificationForCloudLastSuccessText()
        {
            // YD-11: начальная загрузка уведомляет и о вычисляемом тексте даты.
            var lastSuccess = new DateTime(2026, 11, 5, 8, 30, 0, DateTimeKind.Utc);
            _settings.Settings.YandexDiskLastBackupUtc = lastSuccess;
            _backup.CurrentState = new CloudBackupState(
                CloudBackupStatus.UpToDate, null, false, "Облачная копия актуальна.");
            var notified = new List<string?>();
            _vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

            await LoadAsync();

            _vm.CloudLastSuccessText.Should().Be(lastSuccess.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
            notified.Should().Contain(nameof(SettingsViewModel.CloudLastSuccessText));
        }

        [Fact]
        public async Task DisconnectCommand_WithPendingFile_KeepsQueueIndicator()
        {
            // YD-11: файл очереди при отключении намеренно не удаляется —
            // индикатор обязан остаться, а не гаснуть вручную.
            _auth.State = YandexDiskAuthState.Connected;
            _backup.CurrentState = new CloudBackupState(
                CloudBackupStatus.PendingRetry, null, true, "Выгрузка прервана — копия осталась в очереди.");
            await LoadAsync();

            _vm.DisconnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _auth.DisconnectCallCount == 1);

            _vm.CloudHasPendingFile.Should().BeTrue("индикатор очереди соответствует реально существующему файлу");
        }

        [Fact]
        public async Task DisconnectCommand_ClearsConnectionAndLastSuccessDate()
        {
            _auth.State = YandexDiskAuthState.Connected;
            await LoadAsync();
            _settings.Settings.YandexDiskLastBackupUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

            _vm.DisconnectYandexCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _auth.DisconnectCallCount == 1);

            _vm.YandexAuthState.Should().Be(YandexDiskAuthState.NotConnected);
            _vm.CloudLastSuccessUtc.Should().BeNull();
            _settings.Settings.YandexDiskLastBackupUtc.Should().BeNull();
        }

        [Fact]
        public async Task UploadNowCommand_Success_AppliesStateAndShowsToast()
        {
            _auth.State = YandexDiskAuthState.Connected;
            await LoadAsync();
            var lastSuccess = new DateTime(2026, 11, 8, 9, 30, 0, DateTimeKind.Utc);
            _backup.UploadOverride = (_, _) => Task.FromResult(
                new CloudBackupResult(true, new CloudBackupState(CloudBackupStatus.UpToDate, lastSuccess, false, "Облачная копия актуальна.")));

            _vm.UploadNowCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _toast.LastSuccess != null);

            _backup.UploadForceArgs.Should().ContainSingle().Which.Should().BeTrue("ручная загрузка принудительная");
            _vm.CloudLastSuccessUtc.Should().Be(lastSuccess);
            _toast.LastSuccess.Should().Contain("Яндекс Диск");
        }

        [Fact]
        public async Task UploadNowCommand_Failure_ShowsStateMessageWithoutTechnicalDetails()
        {
            _auth.State = YandexDiskAuthState.Connected;
            await LoadAsync();
            _backup.UploadOverride = (_, _) => Task.FromResult(
                new CloudBackupResult(false, new CloudBackupState(CloudBackupStatus.Error, null, true, "Недостаточно места на Яндекс Диске.")));

            _vm.UploadNowCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialog.LastWarning != null);

            _dialog.LastWarning.Should().Be("Недостаточно места на Яндекс Диске.");
            _vm.CloudHasPendingFile.Should().BeTrue();
        }

        [Fact]
        public async Task UploadNowCommand_WhenUnexpectedError_ShowsFriendlyMessage()
        {
            _auth.State = YandexDiskAuthState.Connected;
            await LoadAsync();
            _backup.UploadException = new Exception("HTTP 500 internal details");

            _vm.UploadNowCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialog.LastError != null);

            _dialog.LastError.Should().NotContain("HTTP 500");
            _dialog.LastError.Should().Contain("Не удалось загрузить копию");
        }

        [Fact]
        public async Task UploadNowCommand_RequiresConnection()
        {
            await LoadAsync();
            (_vm.UploadNowCommand.CanExecute(null)).Should().BeFalse(
                "без подключённого аккаунта ручная загрузка недоступна");
        }

        [Fact]
        public async Task BackupStateChanged_UpdatesDisplayedState()
        {
            await LoadAsync();
            _auth.State = YandexDiskAuthState.Connected;

            _backup.RaiseStateChanged(new CloudBackupState(
                CloudBackupStatus.PendingRetry,
                null,
                true,
                "Не удалось выгрузить копию на Яндекс Диск. Повторная попытка будет выполнена позже."));
            await TestWaitHelper.WaitUntilAsync(() => _vm.CloudHasPendingFile);

            _vm.CloudStatusMessage.Should().Contain("Повторная попытка");
        }

        [Fact]
        public async Task AuthStateChanged_UpdatesConnectionState()
        {
            await LoadAsync();
            _auth.State = YandexDiskAuthState.Connected;

            _auth.RaiseStateChanged();
            await TestWaitHelper.WaitUntilAsync(() => _vm.YandexAuthState == YandexDiskAuthState.Connected);
        }

        [Fact]
        public async Task Save_WakesCoordinatorForOutOfScheduleCheck()
        {
            await LoadAsync();

            _vm.YandexDiskBackupEnabled = true; // помечает настройки изменёнными
            _vm.SaveCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _coordinator.RequestCheckCallCount >= 1);

            _settings.Settings.YandexDiskBackupEnabled.Should().BeTrue();
        }

        #region Заглушки зависимостей, не относящихся к облачному сценарию

        private sealed class StubCategoryService : ICategoryService
        {
            public Task<List<Category>> GetAllAsync() => Task.FromResult(new List<Category>());
            public Task<int> GetCountAsync() => Task.FromResult(0);
            public Task<List<Category>> GetActiveAsync() => Task.FromResult(new List<Category>());
            public Task<List<Category>> GetByTypeAsync(OperationType type, bool activeOnly = true) => Task.FromResult(new List<Category>());
            public Task<Category> GetByIdAsync(int id) => Task.FromResult(new Category());
            public Task<Category> AddAsync(Category category) => Task.FromResult(category);
            public Task UpdateAsync(Category category) => Task.CompletedTask;
            public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
            public Task DeactivateAsync(int id) => Task.CompletedTask;
            public Task<int> GetOperationCountAsync(int categoryId) => Task.FromResult(0);
            public Task<bool> ExistsAsync(string name, OperationType type, int? excludeId = null) => Task.FromResult(false);
            public Task ReorderAsync(List<(int Id, int SortOrder)> updates) => Task.CompletedTask;
        }

        private sealed class StubBackupCoordinator : IBackupCoordinator
        {
            public string? SelectBackupFolder(string? initialPath = null) => null;
            public Task<string?> CreateBackupAsync(string? targetPath, CancellationToken ct = default) => Task.FromResult<string?>(null);
            public Task RestoreBackupAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class StubAppInfoService : IAppInfoService
        {
            public Task<AppInfo> GetAppInfoAsync(CancellationToken ct = default)
                => Task.FromResult(new AppInfo("1.0.0-test", "0 КБ", 0, 0, "—"));
        }

        private sealed class StubCreditCardSettingsService : ICreditCardSettingsService
        {
            public Task SyncFromSettingsAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        }

        #endregion
    }
}
