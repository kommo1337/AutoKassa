using AutoKassa.Models;
using AutoKassa.ViewModels;

namespace AutoKassa.Services.Settings
{
    /// <summary>
    /// Реализация маппинга между <see cref="AppSettings"/> и <see cref="SettingsViewModel"/>.
    /// </summary>
    public class SettingsMapper : ISettingsMapper
    {
        /// <inheritdoc />
        public void MapToViewModel(AppSettings source, SettingsViewModel target)
        {
            target.AutoLockEnabled = source.AutoLockEnabled;
            target.AutoLockMinutes = source.AutoLockTimeout;
            target.ShowNotifications = source.ShowNotifications;
            target.InitialBalance = source.InitialBalance;

            target.ShowOperationsInSidebar = source.ShowOperationsInSidebar;
            target.DefaultPageSize = source.DefaultPageSize;
            target.ConfirmDelete = source.ConfirmDelete;
            target.DefaultOperationType = source.DefaultOperationType;
            target.DefaultIncomeCategoryId = source.DefaultIncomeCategoryId;
            target.DefaultExpenseCategoryId = source.DefaultExpenseCategoryId;
            target.DefaultPaymentType = source.DefaultPaymentType;

            target.SelectedDefaultPeriod = source.DefaultPeriodFilter;
            target.AutoGenerateReports = source.AutoGenerateReports;

            target.BackupEnabled = source.BackupEnabled;
            target.AutoBackupDays = source.AutoBackupDays;
            target.BackupPath = source.BackupPath ?? string.Empty;
            target.BackupKeepCount = source.BackupKeepCount;

            target.RequirePasswordOnStartup = source.RequirePasswordOnStartup;
            target.PasswordExpireDays = source.PasswordExpireDays;

            target.CreditCardLimit = source.CreditCardLimit;
            target.CreditCardCurrentDebt = source.CreditCardCurrentDebt;
            target.CreditCardInterestRate = source.CreditCardInterestRate;
            target.CreditCardPaymentDay = source.CreditCardPaymentDay;
            target.CreditCardLastPaymentDate = source.CreditCardLastPaymentDate;
            target.CreditCardMinimumPaymentPercent = source.CreditCardMinimumPaymentPercent;

            // Уведомляем UI о каждом изменённом свойстве вместо массового string.Empty.
            // Это убирает лишний пересчёт скрытых (Collapsed) элементов и ускоряет переключение.
            target.RaisePropertyChanged(nameof(target.AutoLockEnabled));
            target.RaisePropertyChanged(nameof(target.AutoLockMinutes));
            target.RaisePropertyChanged(nameof(target.ShowNotifications));
            target.RaisePropertyChanged(nameof(target.InitialBalance));
            target.RaisePropertyChanged(nameof(target.ShowOperationsInSidebar));
            target.RaisePropertyChanged(nameof(target.DefaultPageSize));
            target.RaisePropertyChanged(nameof(target.ConfirmDelete));
            target.RaisePropertyChanged(nameof(target.DefaultOperationType));
            target.RaisePropertyChanged(nameof(target.DefaultIncomeCategoryId));
            target.RaisePropertyChanged(nameof(target.DefaultExpenseCategoryId));
            target.RaisePropertyChanged(nameof(target.DefaultPaymentType));
            target.RaisePropertyChanged(nameof(target.SelectedDefaultPeriod));
            target.RaisePropertyChanged(nameof(target.AutoGenerateReports));
            target.RaisePropertyChanged(nameof(target.BackupEnabled));
            target.RaisePropertyChanged(nameof(target.AutoBackupDays));
            target.RaisePropertyChanged(nameof(target.BackupPath));
            target.RaisePropertyChanged(nameof(target.BackupKeepCount));
            target.RaisePropertyChanged(nameof(target.RequirePasswordOnStartup));
            target.RaisePropertyChanged(nameof(target.PasswordExpireDays));
            target.RaisePropertyChanged(nameof(target.CreditCardLimit));
            target.RaisePropertyChanged(nameof(target.CreditCardCurrentDebt));
            target.RaisePropertyChanged(nameof(target.CreditCardInterestRate));
            target.RaisePropertyChanged(nameof(target.CreditCardPaymentDay));
            target.RaisePropertyChanged(nameof(target.CreditCardLastPaymentDate));
            target.RaisePropertyChanged(nameof(target.CreditCardMinimumPaymentPercent));
            target.RaisePropertyChanged(nameof(target.HasUnsavedChanges));
        }

        /// <inheritdoc />
        public void MapToModel(SettingsViewModel source, AppSettings target)
        {
            target.AutoLockEnabled = source.AutoLockEnabled;
            target.AutoLockTimeout = source.AutoLockMinutes;
            target.ShowNotifications = source.ShowNotifications;
            target.InitialBalance = source.InitialBalance;

            target.ShowOperationsInSidebar = source.ShowOperationsInSidebar;
            target.DefaultPageSize = source.DefaultPageSize;
            target.ConfirmDelete = source.ConfirmDelete;
            target.DefaultOperationType = source.DefaultOperationType;
            target.DefaultIncomeCategoryId = source.DefaultIncomeCategoryId;
            target.DefaultExpenseCategoryId = source.DefaultExpenseCategoryId;
            target.DefaultPaymentType = source.DefaultPaymentType;

            target.DefaultPeriodFilter = source.SelectedDefaultPeriod;
            target.AutoGenerateReports = source.AutoGenerateReports;

            target.BackupEnabled = source.BackupEnabled;
            target.AutoBackupDays = source.AutoBackupDays;
            target.BackupPath = SettingsService.SanitizeBackupPath(source.BackupPath);
            target.BackupKeepCount = source.BackupKeepCount;

            target.RequirePasswordOnStartup = source.RequirePasswordOnStartup;
            target.PasswordExpireDays = source.PasswordExpireDays;

            target.CreditCardLimit = source.CreditCardLimit;
            target.CreditCardCurrentDebt = source.CreditCardCurrentDebt;
            target.CreditCardInterestRate = source.CreditCardInterestRate;
            target.CreditCardPaymentDay = source.CreditCardPaymentDay;
            target.CreditCardLastPaymentDate = source.CreditCardLastPaymentDate;
            target.CreditCardMinimumPaymentPercent = source.CreditCardMinimumPaymentPercent;
        }

        /// <inheritdoc />
        public AppSettings Clone(AppSettings source)
        {
            return new AppSettings
            {
                Id = source.Id,
                PasswordHash = source.PasswordHash,
                SecurityQuestionId = source.SecurityQuestionId,
                SecurityAnswerHash = source.SecurityAnswerHash,
                CustomSecurityQuestion = source.CustomSecurityQuestion,
                AutoLockTimeout = source.AutoLockTimeout,
                AutoLockEnabled = source.AutoLockEnabled,
                Theme = source.Theme,
                DefaultPeriodFilter = source.DefaultPeriodFilter,
                ShowNotifications = source.ShowNotifications,
                ShowOperationsInSidebar = source.ShowOperationsInSidebar,
                DefaultPageSize = source.DefaultPageSize,
                ConfirmDelete = source.ConfirmDelete,
                AutoGenerateReports = source.AutoGenerateReports,
                BackupEnabled = source.BackupEnabled,
                AutoBackupDays = source.AutoBackupDays,
                BackupFrequency = source.BackupFrequency,
                BackupKeepCount = source.BackupKeepCount,
                BackupPath = source.BackupPath,
                RequirePasswordOnStartup = source.RequirePasswordOnStartup,
                PasswordExpireDays = source.PasswordExpireDays,
                Language = source.Language,
                WindowWidth = source.WindowWidth,
                WindowHeight = source.WindowHeight,
                DefaultOperationType = source.DefaultOperationType,
                DefaultIncomeCategoryId = source.DefaultIncomeCategoryId,
                DefaultExpenseCategoryId = source.DefaultExpenseCategoryId,
                DefaultPaymentType = source.DefaultPaymentType,
                InitialBalance = source.InitialBalance,
                CreditCardLimit = source.CreditCardLimit,
                CreditCardCurrentDebt = source.CreditCardCurrentDebt,
                CreditCardInterestRate = source.CreditCardInterestRate,
                CreditCardPaymentDay = source.CreditCardPaymentDay,
                CreditCardLastPaymentDate = source.CreditCardLastPaymentDate,
                CreditCardMinimumPaymentPercent = source.CreditCardMinimumPaymentPercent
            };
        }
    }
}
