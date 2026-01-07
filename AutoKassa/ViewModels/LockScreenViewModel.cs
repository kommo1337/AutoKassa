using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Services;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для экрана блокировки
    /// </summary>
    public class LockScreenViewModel : ViewModelBase
    {
        private readonly IPasswordService _passwordService;
        private readonly ISettingsService _settingsService;
        private string _password;
        private string _errorMessage;
        private bool _isPasswordRecoveryMode;

        public LockScreenViewModel(
            IPasswordService passwordService,
            ISettingsService settingsService)
        {
            _passwordService = passwordService;
            _settingsService = settingsService;

            // Команды
            UnlockCommand = new RelayCommand(_ => Unlock(), _ => CanUnlock());
            ShowPasswordRecoveryCommand = new RelayCommand(_ => ShowPasswordRecovery());
            CancelPasswordRecoveryCommand = new RelayCommand(_ => CancelPasswordRecovery());
        }

        #region Свойства

        /// <summary>
        /// Введенный пароль
        /// </summary>
        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    ErrorMessage = null; // Сбрасываем ошибку при вводе
                }
            }
        }

        /// <summary>
        /// Сообщение об ошибке
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Режим восстановления пароля
        /// </summary>
        public bool IsPasswordRecoveryMode
        {
            get => _isPasswordRecoveryMode;
            set => SetProperty(ref _isPasswordRecoveryMode, value);
        }

        #endregion

        #region Команды

        public ICommand UnlockCommand { get; }
        public ICommand ShowPasswordRecoveryCommand { get; }
        public ICommand CancelPasswordRecoveryCommand { get; }

        #endregion

        #region Методы

        private bool CanUnlock()
        {
            return !string.IsNullOrEmpty(Password);
        }

        private void Unlock()
        {
            if (string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Введите пароль";
                OnPasswordError?.Invoke(); // Анимация тряски
                return;
            }

            var settings = _settingsService.GetSettings();

            if (_passwordService.VerifyPassword(Password, settings.PasswordHash))
            {
                // Пароль верный - разблокируем
                Password = string.Empty;
                ErrorMessage = null;
                OnUnlocked?.Invoke();
            }
            else
            {
                // Неверный пароль
                ErrorMessage = "Неверный пароль. Попробуйте снова.";
                Password = string.Empty;
                OnPasswordError?.Invoke(); // Анимация тряски
            }
        }

        private void ShowPasswordRecovery()
        {
            IsPasswordRecoveryMode = true;
        }

        private void CancelPasswordRecovery()
        {
            IsPasswordRecoveryMode = false;
            ErrorMessage = null;
        }

        #endregion

        #region События

        /// <summary>
        /// Событие успешной разблокировки
        /// </summary>
        public System.Action OnUnlocked { get; set; }

        /// <summary>
        /// Событие ошибки пароля (для анимации)
        /// </summary>
        public System.Action OnPasswordError { get; set; }

        #endregion
    }
}