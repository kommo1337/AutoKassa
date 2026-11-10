using AutoKassa.Models;
using AutoKassa.Services;
using AutoKassa.Services.Settings;
using AutoKassa.Tests.Infrastructure;
using AutoKassa.Tests.Services.Backup;
using AutoKassa.Tests.Services.Backup.YandexDisk;
using AutoKassa.ViewModels;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class SettingsMapperTests
    {
        private static SettingsViewModel CreateViewModel()
        {
            // Конструктор SettingsViewModel подписывается на события облачных сервисов —
            // передаём фейки вместо null, подписка на null бросила бы исключение.
            return new SettingsViewModel(
                null!, null!, null!, null!, new FakeDataChangeService(),
                null!, null!, null!, null!, null!, null!,
                new FakeYandexDiskAuthService(), new FakeYandexDiskBackupService(),
                new FakeAutomaticBackupService(), null!);
        }

        [Fact]
        public void MapToViewModel_ShouldCopyPrimitiveValues()
        {
            var mapper = new SettingsMapper();
            var vm = CreateViewModel();
            var settings = new AppSettings
            {
                AutoLockEnabled = true,
                AutoLockTimeout = 7,
                ShowNotifications = false,
                InitialBalance = 1234.56m,
                ShowOperationsInSidebar = true,
                DefaultPageSize = 42,
                ConfirmDelete = false,
                DefaultOperationType = 2,
                DefaultIncomeCategoryId = 5,
                DefaultExpenseCategoryId = 8,
                DefaultPaymentType = 3,
                DefaultPeriodFilter = "Month",
                AutoGenerateReports = true,
                BackupEnabled = true,
                AutoBackupDays = 14,
                BackupPath = @"C:\Backups",
                BackupKeepCount = 5,
                YandexDiskBackupEnabled = true,
                RequirePasswordOnStartup = true,
                PasswordExpireDays = 90,
                CreditCardLimit = 200000,
                CreditCardCurrentDebt = 50000,
                CreditCardInterestRate = 19.9m,
                CreditCardPaymentDay = 25,
                CreditCardLastPaymentDate = new DateTime(2026, 6, 15),
                CreditCardMinimumPaymentPercent = 7.5m
            };

            mapper.MapToViewModel(settings, vm);

            vm.AutoLockEnabled.Should().BeTrue();
            vm.AutoLockMinutes.Should().Be(7);
            vm.ShowNotifications.Should().BeFalse();
            vm.InitialBalance.Should().Be(1234.56m);
            vm.ShowOperationsInSidebar.Should().BeTrue();
            vm.DefaultPageSize.Should().Be(42);
            vm.ConfirmDelete.Should().BeFalse();
            vm.DefaultOperationType.Should().Be(2);
            vm.DefaultIncomeCategoryId.Should().Be(5);
            vm.DefaultExpenseCategoryId.Should().Be(8);
            vm.DefaultPaymentType.Should().Be(3);
            vm.SelectedDefaultPeriod.Should().Be("Month");
            vm.AutoGenerateReports.Should().BeTrue();
            vm.BackupEnabled.Should().BeTrue();
            vm.AutoBackupDays.Should().Be(14);
            vm.BackupPath.Should().Be(@"C:\Backups");
            vm.BackupKeepCount.Should().Be(5);
            vm.YandexDiskBackupEnabled.Should().BeTrue();
            vm.RequirePasswordOnStartup.Should().BeTrue();
            vm.PasswordExpireDays.Should().Be(90);
            vm.CreditCardLimit.Should().Be(200000);
            vm.CreditCardCurrentDebt.Should().Be(50000);
            vm.CreditCardInterestRate.Should().Be(19.9m);
            vm.CreditCardPaymentDay.Should().Be(25);
            vm.CreditCardLastPaymentDate.Should().Be(new DateTime(2026, 6, 15));
            vm.CreditCardMinimumPaymentPercent.Should().Be(7.5m);
        }

        [Fact]
        public void MapToModel_ShouldCopyValuesBackToAppSettings()
        {
            var mapper = new SettingsMapper();
            var vm = CreateViewModel();
            vm.AutoLockEnabled = false;
            vm.AutoLockMinutes = 3;
            vm.DefaultPageSize = 100;
            vm.BackupPath = AppDomain.CurrentDomain.BaseDirectory;
            vm.CreditCardLimit = 300000;
            vm.YandexDiskBackupEnabled = true;

            var settings = new AppSettings();
            mapper.MapToModel(vm, settings);

            settings.AutoLockEnabled.Should().BeFalse();
            settings.AutoLockTimeout.Should().Be(3);
            settings.DefaultPageSize.Should().Be(100);
            settings.BackupPath.Should().Be(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory));
            settings.CreditCardLimit.Should().Be(300000);
            settings.YandexDiskBackupEnabled.Should().BeTrue();
        }

        [Fact]
        public void MapToViewModel_NullBackupPath_ShouldMapToEmptyString()
        {
            var mapper = new SettingsMapper();
            var vm = CreateViewModel();
            var settings = new AppSettings { BackupPath = null };

            mapper.MapToViewModel(settings, vm);

            vm.BackupPath.Should().BeEmpty();
        }

        [Fact]
        public void Clone_ShouldCreateIndependentCopy()
        {
            var mapper = new SettingsMapper();
            var original = new AppSettings
            {
                Id = 1,
                AutoLockTimeout = 10,
                BackupPath = @"E:\Backups",
                YandexDiskBackupEnabled = true,
                YandexDiskLastBackupUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc)
            };

            var copy = mapper.Clone(original);

            copy.Should().NotBeSameAs(original);
            copy.Id.Should().Be(original.Id);
            copy.AutoLockTimeout.Should().Be(original.AutoLockTimeout);
            copy.BackupPath.Should().Be(original.BackupPath);
            copy.YandexDiskBackupEnabled.Should().BeTrue();
            copy.YandexDiskLastBackupUtc.Should().Be(original.YandexDiskLastBackupUtc);

            copy.AutoLockTimeout = 99;
            original.AutoLockTimeout.Should().Be(10);
        }
    }
}
