using AutoKassa.ViewModels;

namespace AutoKassa.Services.Settings
{
    /// <summary>
    /// Реализация валидации значений полей настроек.
    /// </summary>
    public class SettingsValidator : ISettingsValidator
    {
        /// <inheritdoc />
        public IReadOnlyList<string> Validate(SettingsViewModel vm)
        {
            var errors = new List<string>();

            if (vm.AutoLockMinutes < 1 || vm.AutoLockMinutes > 60)
                errors.Add("Время автоблокировки должно быть от 1 до 60 минут");

            if (vm.DefaultPageSize < 10 || vm.DefaultPageSize > 100)
                errors.Add("Количество записей на странице должно быть от 10 до 100");

            if (vm.AutoBackupDays < 1 || vm.AutoBackupDays > 365)
                errors.Add("Интервал резервного копирования должен быть от 1 до 365 дней");

            if (vm.PasswordExpireDays < 0 || vm.PasswordExpireDays > 365)
                errors.Add("Срок действия пароля должен быть от 0 до 365 дней");

            if (vm.BackupKeepCount < 1 || vm.BackupKeepCount > 100)
                errors.Add("Количество хранимых копий должно быть от 1 до 100");

            if (vm.CreditCardLimit < 0)
                errors.Add("Кредитный лимит не может быть отрицательным");

            if (vm.CreditCardCurrentDebt < 0)
                errors.Add("Текущий долг не может быть отрицательным");

            if (vm.CreditCardInterestRate < 0)
                errors.Add("Процентная ставка не может быть отрицательной");

            if (vm.CreditCardPaymentDay < 1 || vm.CreditCardPaymentDay > 31)
                errors.Add("День платежа должен быть от 1 до 31");

            if (vm.CreditCardMinimumPaymentPercent < 0 || vm.CreditCardMinimumPaymentPercent > 100)
                errors.Add("Процент минимального платежа должен быть от 0 до 100");

            if (!string.IsNullOrEmpty(vm.BackupPath) && vm.BackupPath.Length > 500)
                errors.Add("Путь резервного копирования не должен превышать 500 символов");

            return errors;
        }
    }
}
