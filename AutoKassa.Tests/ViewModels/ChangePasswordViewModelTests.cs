using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using AutoKassa.ViewModels;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.ViewModels
{
    public class ChangePasswordViewModelTests : IDisposable
    {
        private readonly TestDbContextFactory _factory;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly SettingsService _settingsService;
        private readonly FakeDialogService _dialogService;
        private readonly FakeToastNotificationService _toastService;

        public ChangePasswordViewModelTests()
        {
            (_factory, _conn) = TestDatabase.CreateWithFactory();
            _settingsService = new SettingsService(_factory);
            _dialogService = new FakeDialogService();
            _toastService = new FakeToastNotificationService();
        }

        public void Dispose()
        {
            _conn.Dispose();
        }

        private ChangePasswordViewModel CreateViewModel() =>
            new ChangePasswordViewModel(_settingsService, _dialogService, _toastService);

        [Fact]
        public void NewPassword_ShorterThan6_ShowsError()
        {
            var vm = CreateViewModel();

            vm.NewPassword = "12345";

            vm.NewPasswordError.Should().Contain("минимум 6 символов");
        }

        [Fact]
        public void NewPassword_Exactly6_NoLengthError()
        {
            var vm = CreateViewModel();
            vm.CurrentPassword = "oldpass1";

            vm.NewPassword = "123456";

            vm.NewPasswordError.Should().BeNull();
        }

        [Fact]
        public void NewPassword_SameAsCurrent_ShowsError()
        {
            var vm = CreateViewModel();
            vm.CurrentPassword = "samepass1";

            vm.NewPassword = "samepass1";

            vm.NewPasswordError.Should().Contain("должен отличаться");
        }

        [Fact]
        public void ConfirmPassword_Mismatch_ShowsError()
        {
            var vm = CreateViewModel();
            vm.CurrentPassword = "oldpass1";
            vm.NewPassword = "newpass1";

            vm.ConfirmPassword = "otherpass";

            vm.ConfirmPasswordError.Should().Contain("не совпадают");
        }
    }
}
