using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using AutoKassa.ViewModels;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.ViewModels
{
    public class LockScreenViewModelTests : IDisposable
    {
        private readonly TestDbContextFactory _factory;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly SettingsService _settingsService;
        private readonly PasswordService _passwordService;
        private readonly FakeDialogService _dialogService;

        public LockScreenViewModelTests()
        {
            (_factory, _conn) = TestDatabase.CreateWithFactory();
            _settingsService = new SettingsService(_factory);
            _passwordService = new PasswordService();
            _dialogService = new FakeDialogService();
        }

        public void Dispose()
        {
            _conn.Dispose();
        }

        private async Task SeedPassword(string password = "secret1")
        {
            await _settingsService.SetPasswordAsync(
                _passwordService.HashPassword(password),
                SecurityQuestion.MotherMaidenName,
                _passwordService.HashPassword("ответ"));
        }

        private LockScreenViewModel CreateViewModel() =>
            new LockScreenViewModel(
                _passwordService,
                _settingsService,
                new PasswordRecoveryViewModel(_passwordService, _settingsService, _dialogService));

        [Fact]
        public async Task UnlockCommand_CanExecute_RequiresPassword()
        {
            await SeedPassword();
            var vm = CreateViewModel();

            vm.UnlockCommand.CanExecute(null).Should().BeFalse();

            vm.Password = "x";
            vm.UnlockCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public async Task Unlock_WrongPassword_SetsErrorAndFiresOnPasswordError()
        {
            await SeedPassword();
            var vm = CreateViewModel();
            var errorFired = false;
            var unlocked = false;
            vm.OnPasswordError = () => errorFired = true;
            vm.OnUnlocked = () => unlocked = true;

            vm.Password = "wrongpass";
            vm.UnlockCommand.Execute(null);

            vm.ErrorMessage.Should().Contain("Неверный пароль");
            vm.Password.Should().BeEmpty();
            errorFired.Should().BeTrue();
            unlocked.Should().BeFalse();
        }

        [Fact]
        public async Task Unlock_CorrectPassword_FiresOnUnlockedAndClearsFields()
        {
            await SeedPassword();
            var vm = CreateViewModel();
            var unlocked = false;
            vm.OnUnlocked = () => unlocked = true;

            vm.Password = "secret1";
            vm.UnlockCommand.Execute(null);

            unlocked.Should().BeTrue();
            vm.Password.Should().BeEmpty();
            vm.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task ShowPasswordRecovery_EntersModeAndResetsRecoveryState()
        {
            await SeedPassword();
            var vm = CreateViewModel();
            vm.Recovery.Answer = "мусор";
            vm.Recovery.IsAnswerVerified = true;

            vm.ShowPasswordRecoveryCommand.Execute(null);

            vm.IsPasswordRecoveryMode.Should().BeTrue();
            vm.Recovery.Answer.Should().BeEmpty();
            vm.Recovery.IsAnswerVerified.Should().BeFalse();
        }

        [Fact]
        public async Task UnlockCommand_InRecoveryMode_CannotExecute()
        {
            await SeedPassword();
            var vm = CreateViewModel();
            vm.Password = "secret1";
            vm.ShowPasswordRecoveryCommand.Execute(null);

            vm.UnlockCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public async Task CancelPasswordRecovery_ExitsMode()
        {
            await SeedPassword();
            var vm = CreateViewModel();
            vm.ShowPasswordRecoveryCommand.Execute(null);

            vm.CancelPasswordRecoveryCommand.Execute(null);

            vm.IsPasswordRecoveryMode.Should().BeFalse();
            vm.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task RecoveryPasswordReset_ReturnsToLoginPanel()
        {
            await SeedPassword();
            var vm = CreateViewModel();
            vm.Password = "что-то";
            vm.ShowPasswordRecoveryCommand.Execute(null);

            // Симулируем успешную смену пароля через восстановление
            vm.Recovery.OnPasswordReset?.Invoke();

            vm.IsPasswordRecoveryMode.Should().BeFalse();
            vm.Password.Should().BeEmpty();
            vm.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task Dispose_DetachesRecoveryHandlers()
        {
            await SeedPassword();
            var vm = CreateViewModel();

            vm.Dispose();

            vm.Recovery.OnPasswordReset.Should().BeNull();
            vm.Recovery.OnCancelled.Should().BeNull();
        }
    }
}
