using AutoKassa.Services;
using AutoKassa.Services.Settings;
using AutoKassa.Tests.Infrastructure;
using AutoKassa.ViewModels;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class SettingsValidatorTests
    {
        private static SettingsViewModel CreateValidViewModel()
        {
            return new SettingsViewModel(
                null!, null!, null!, null!, new FakeDataChangeService(),
                null!, null!, null!, null!, null!, null!)
            {
                AutoLockMinutes = 5,
                DefaultPageSize = 50,
                AutoBackupDays = 7,
                PasswordExpireDays = 30,
                BackupKeepCount = 10,
                CreditCardLimit = 100000,
                CreditCardCurrentDebt = 0,
                CreditCardInterestRate = 15,
                CreditCardPaymentDay = 15,
                CreditCardMinimumPaymentPercent = 10
            };
        }

        [Fact]
        public void Validate_ValidValues_ReturnsNoErrors()
        {
            var validator = new SettingsValidator();
            var vm = CreateValidViewModel();

            var errors = validator.Validate(vm);

            errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(61)]
        public void Validate_AutoLockMinutesOutOfRange_ReturnsError(int value)
        {
            var validator = new SettingsValidator();
            var vm = CreateValidViewModel();
            vm.AutoLockMinutes = value;

            var errors = validator.Validate(vm);

            errors.Should().ContainSingle(e => e.Contains("автоблокировки"));
        }

        [Theory]
        [InlineData(9)]
        [InlineData(101)]
        public void Validate_DefaultPageSizeOutOfRange_ReturnsError(int value)
        {
            var validator = new SettingsValidator();
            var vm = CreateValidViewModel();
            vm.DefaultPageSize = value;

            var errors = validator.Validate(vm);

            errors.Should().ContainSingle(e => e.Contains("записей на странице"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(366)]
        public void Validate_AutoBackupDaysOutOfRange_ReturnsError(int value)
        {
            var validator = new SettingsValidator();
            var vm = CreateValidViewModel();
            vm.AutoBackupDays = value;

            var errors = validator.Validate(vm);

            errors.Should().ContainSingle(e => e.Contains("резервного копирования"));
        }

        [Fact]
        public void Validate_NegativeCreditCardLimit_ReturnsError()
        {
            var validator = new SettingsValidator();
            var vm = CreateValidViewModel();
            vm.CreditCardLimit = -1;

            var errors = validator.Validate(vm);

            errors.Should().ContainSingle(e => e.Contains("Кредитный лимит"));
        }

        [Fact]
        public void Validate_CreditCardPaymentDayOutOfRange_ReturnsError()
        {
            var validator = new SettingsValidator();
            var vm = CreateValidViewModel();
            vm.CreditCardPaymentDay = 32;

            var errors = validator.Validate(vm);

            errors.Should().ContainSingle(e => e.Contains("День платежа"));
        }

        [Fact]
        public void Validate_MultipleInvalidValues_ReturnsMultipleErrors()
        {
            var validator = new SettingsValidator();
            var vm = CreateValidViewModel();
            vm.AutoLockMinutes = 0;
            vm.DefaultPageSize = 0;
            vm.CreditCardPaymentDay = 0;

            var errors = validator.Validate(vm);

            errors.Should().HaveCount(3);
        }

        [Fact]
        public void Validate_TooLongBackupPath_ReturnsError()
        {
            var validator = new SettingsValidator();
            var vm = CreateValidViewModel();
            vm.BackupPath = new string('X', 501);

            var errors = validator.Validate(vm);

            errors.Should().ContainSingle(e => e.Contains("500"));
        }
    }
}
