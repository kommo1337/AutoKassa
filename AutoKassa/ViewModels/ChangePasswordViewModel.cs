using AutoKassa.Helpers;
using AutoKassa.Services;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для смены пароля
    /// </summary>
    public class ChangePasswordViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;

        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;

        public ChangePasswordViewModel(
            ISettingsService settingsService,
            IDialogService dialogService,
            IToastNotificationService toastService)
        {
            _settingsService = settingsService;
            _dialogService = dialogService;
            _toastService = toastService;

            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        #region Свойства

        public string CurrentPassword
        {
            get => _currentPassword;
            set
            {
                if (SetProperty(ref _currentPassword, value))
                {
                    ValidateCurrentPassword();
                }
            }
        }

        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (SetProperty(ref _newPassword, value))
                {
                    ValidateNewPassword();
                    ValidateConfirmPassword();
                }
            }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (SetProperty(ref _confirmPassword, value))
                {
                    ValidateConfirmPassword();
                }
            }
        }

        // Ошибки хранятся в INotifyDataErrorInfo (ViewModelBase),
        // свойства — computed для привязки в XAML
        public string CurrentPasswordError => GetFirstError(nameof(CurrentPassword));
        public string NewPasswordError => GetFirstError(nameof(NewPassword));
        public string ConfirmPasswordError => GetFirstError(nameof(ConfirmPassword));

        /// <summary>
        /// Событие закрытия окна
        /// </summary>
        public Action<bool>? OnClose { get; set; }

        #endregion

        #region Команды

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Методы

        private void ValidateCurrentPassword()
        {
            if (string.IsNullOrEmpty(CurrentPassword))
            {
                SetErrors(nameof(CurrentPassword), new[] { "Введите текущий пароль" });
            }
            else
            {
                ClearErrors(nameof(CurrentPassword));
            }
            OnPropertyChanged(nameof(CurrentPasswordError));
        }

        private void ValidateNewPassword()
        {
            if (string.IsNullOrEmpty(NewPassword))
            {
                SetErrors(nameof(NewPassword), new[] { "Введите новый пароль" });
            }
            else if (NewPassword.Length < 4)
            {
                SetErrors(nameof(NewPassword), new[] { "Пароль должен содержать минимум 4 символа" });
            }
            else if (NewPassword == CurrentPassword)
            {
                SetErrors(nameof(NewPassword), new[] { "Новый пароль должен отличаться от текущего" });
            }
            else
            {
                ClearErrors(nameof(NewPassword));
            }
            OnPropertyChanged(nameof(NewPasswordError));
        }

        private void ValidateConfirmPassword()
        {
            if (string.IsNullOrEmpty(ConfirmPassword))
            {
                SetErrors(nameof(ConfirmPassword), new[] { "Подтвердите пароль" });
            }
            else if (ConfirmPassword != NewPassword)
            {
                SetErrors(nameof(ConfirmPassword), new[] { "Пароли не совпадают" });
            }
            else
            {
                ClearErrors(nameof(ConfirmPassword));
            }
            OnPropertyChanged(nameof(ConfirmPasswordError));
        }

        private bool CanSave()
        {
            return !string.IsNullOrEmpty(CurrentPassword) &&
                   !string.IsNullOrEmpty(NewPassword) &&
                   !string.IsNullOrEmpty(ConfirmPassword) &&
                   !HasErrors;
        }

        private async Task SaveAsync()
        {
            try
            {
                // Проверяем текущий пароль
                var settings = await _settingsService.GetSettingsAsync();
                if (!BCrypt.Net.BCrypt.Verify(CurrentPassword, settings.PasswordHash))
                {
                    SetErrors(nameof(CurrentPassword), new[] { "Неверный текущий пароль" });
                    OnPropertyChanged(nameof(CurrentPasswordError));
                    return;
                }

                // Хешируем и сохраняем новый пароль
                var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                await _settingsService.UpdatePasswordAsync(newPasswordHash);

                _toastService.ShowSuccess("Пароль успешно изменен");
                OnClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка смены пароля: {ex.Message}");
            }
        }

        private void Cancel()
        {
            OnClose?.Invoke(false);
        }

        #endregion
    }
}
