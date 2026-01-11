using AutoKassa.Helpers;
using AutoKassa.Services;
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
        private string _currentPasswordError = string.Empty;
        private string _newPasswordError = string.Empty;
        private string _confirmPasswordError = string.Empty;

        public ChangePasswordViewModel(
            ISettingsService settingsService,
            IDialogService dialogService,
            IToastNotificationService toastService)
        {
            _settingsService = settingsService;
            _dialogService = dialogService;
            _toastService = toastService;

            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
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

        public string CurrentPasswordError
        {
            get => _currentPasswordError;
            set => SetProperty(ref _currentPasswordError, value);
        }

        public string NewPasswordError
        {
            get => _newPasswordError;
            set => SetProperty(ref _newPasswordError, value);
        }

        public string ConfirmPasswordError
        {
            get => _confirmPasswordError;
            set => SetProperty(ref _confirmPasswordError, value);
        }

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
                CurrentPasswordError = "Введите текущий пароль";
            }
            else
            {
                CurrentPasswordError = string.Empty;
            }
        }

        private void ValidateNewPassword()
        {
            if (string.IsNullOrEmpty(NewPassword))
            {
                NewPasswordError = "Введите новый пароль";
            }
            else if (NewPassword.Length < 4)
            {
                NewPasswordError = "Пароль должен содержать минимум 4 символа";
            }
            else if (NewPassword == CurrentPassword)
            {
                NewPasswordError = "Новый пароль должен отличаться от текущего";
            }
            else
            {
                NewPasswordError = string.Empty;
            }
        }

        private void ValidateConfirmPassword()
        {
            if (string.IsNullOrEmpty(ConfirmPassword))
            {
                ConfirmPasswordError = "Подтвердите пароль";
            }
            else if (ConfirmPassword != NewPassword)
            {
                ConfirmPasswordError = "Пароли не совпадают";
            }
            else
            {
                ConfirmPasswordError = string.Empty;
            }
        }

        private bool CanSave()
        {
            return !string.IsNullOrEmpty(CurrentPassword) &&
                   !string.IsNullOrEmpty(NewPassword) &&
                   !string.IsNullOrEmpty(ConfirmPassword) &&
                   string.IsNullOrEmpty(CurrentPasswordError) &&
                   string.IsNullOrEmpty(NewPasswordError) &&
                   string.IsNullOrEmpty(ConfirmPasswordError);
        }

        private void Save()
        {
            try
            {
                // Проверяем текущий пароль
                var settings = _settingsService.GetSettings();
                if (!BCrypt.Net.BCrypt.Verify(CurrentPassword, settings.PasswordHash))
                {
                    CurrentPasswordError = "Неверный текущий пароль";
                    return;
                }

                // Хешируем и сохраняем новый пароль
                var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                _settingsService.UpdatePassword(newPasswordHash);

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
