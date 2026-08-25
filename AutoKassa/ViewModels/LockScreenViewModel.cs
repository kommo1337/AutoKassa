using AutoKassa.Helpers;
using AutoKassa.Services;
using System.Windows.Input;

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
            ISettingsService settingsService,
            PasswordRecoveryViewModel passwordRecoveryViewModel)
        {
            _passwordService = passwordService;
            _settingsService = settingsService;

            // Дочерняя ViewModel восстановления пароля
            Recovery = passwordRecoveryViewModel;
            Recovery.OnPasswordReset = OnRecoveryPasswordReset;
            Recovery.OnCancelled = CancelPasswordRecovery;

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

        /// <summary>
        /// ViewModel панели восстановления пароля
        /// </summary>
        public PasswordRecoveryViewModel Recovery { get; }

        #endregion

        #region Команды

        public ICommand UnlockCommand { get; }
        public ICommand ShowPasswordRecoveryCommand { get; }
        public ICommand CancelPasswordRecoveryCommand { get; }

        #endregion

        #region Методы

        private bool CanUnlock()
        {
            return !string.IsNullOrEmpty(Password) && !IsPasswordRecoveryMode;
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
                // Неверный пароль. Сначала очищаем поле — сеттер Password сбрасывает
                // ErrorMessage, поэтому сообщение ставим после очистки.
                Password = string.Empty;
                ErrorMessage = "Неверный пароль. Попробуйте снова.";
                OnPasswordError?.Invoke(); // Анимация тряски
            }
        }

        private void ShowPasswordRecovery()
        {
            Recovery.Reset(); // Сбрасываем состояние предыдущей попытки
            IsPasswordRecoveryMode = true;
        }

        private void CancelPasswordRecovery()
        {
            IsPasswordRecoveryMode = false;
            ErrorMessage = null;
        }

        /// <summary>
        /// Пароль успешно изменён через восстановление —
        /// возвращаемся к панели входа для разблокировки новым паролем
        /// </summary>
        private void OnRecoveryPasswordReset()
        {
            IsPasswordRecoveryMode = false;
            Password = string.Empty;
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

        protected override void OnDispose()
        {
            Recovery.OnPasswordReset = null;
            Recovery.OnCancelled = null;
            Recovery.Dispose();
        }
    }
}