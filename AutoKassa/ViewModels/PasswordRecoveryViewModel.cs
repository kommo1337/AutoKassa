using AutoKassa.Helpers;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для восстановления пароля
    /// </summary>
    public class PasswordRecoveryViewModel : ViewModelBase
    {
        private readonly IPasswordService _passwordService;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private string _questionText;
        private string _answer;
        private string _newPassword;
        private string _confirmNewPassword;
        private string _errorMessage;
        private bool _isAnswerVerified;

        public PasswordRecoveryViewModel(
            IPasswordService passwordService,
            ISettingsService settingsService,
            IDialogService dialogService)
        {
            _passwordService = passwordService;
            _settingsService = settingsService;
            _dialogService = dialogService;

            LoadSecurityQuestion();

            // Команды
            VerifyAnswerCommand = new RelayCommand(_ => VerifyAnswer(), _ => CanVerifyAnswer());
            ResetPasswordCommand = new RelayCommand(_ => ResetPassword(), _ => CanResetPassword());
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        #region Свойства

        /// <summary>
        /// Текст секретного вопроса
        /// </summary>
        public string QuestionText
        {
            get => _questionText;
            set => SetProperty(ref _questionText, value);
        }

        /// <summary>
        /// Ответ на секретный вопрос
        /// </summary>
        public string Answer
        {
            get => _answer;
            set
            {
                if (SetProperty(ref _answer, value))
                {
                    ErrorMessage = null;
                }
            }
        }

        /// <summary>
        /// Новый пароль
        /// </summary>
        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (SetProperty(ref _newPassword, value))
                {
                    ErrorMessage = null;
                }
            }
        }

        /// <summary>
        /// Подтверждение нового пароля
        /// </summary>
        public string ConfirmNewPassword
        {
            get => _confirmNewPassword;
            set
            {
                if (SetProperty(ref _confirmNewPassword, value))
                {
                    ErrorMessage = null;
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
        /// Ответ проверен успешно
        /// </summary>
        public bool IsAnswerVerified
        {
            get => _isAnswerVerified;
            set => SetProperty(ref _isAnswerVerified, value);
        }

        #endregion

        #region Команды

        public ICommand VerifyAnswerCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Методы

        private void LoadSecurityQuestion()
        {
            var settings = _settingsService.GetSettings();

            if (settings.SecurityQuestionId.HasValue)
            {
                if (settings.SecurityQuestionId == SecurityQuestion.Custom)
                {
                    QuestionText = settings.CustomSecurityQuestion ?? "Секретный вопрос не установлен";
                }
                else
                {
                    QuestionText = SecurityQuestionHelper.GetQuestionText(settings.SecurityQuestionId.Value);
                }
            }
            else
            {
                QuestionText = "Секретный вопрос не установлен";
            }
        }

        private bool CanVerifyAnswer()
        {
            return !string.IsNullOrEmpty(Answer) && !IsAnswerVerified;
        }

        private void VerifyAnswer()
        {
            if (string.IsNullOrEmpty(Answer))
            {
                ErrorMessage = "Введите ответ";
                return;
            }

            var settings = _settingsService.GetSettings();
            var normalizedAnswer = Answer.Trim().ToLower();

            if (_passwordService.VerifyPassword(normalizedAnswer, settings.SecurityAnswerHash))
            {
                // Ответ верный
                IsAnswerVerified = true;
                ErrorMessage = null;
            }
            else
            {
                // Неверный ответ
                ErrorMessage = "Неверный ответ. Попробуйте снова.";
                Answer = string.Empty;
            }
        }

        private bool CanResetPassword()
        {
            return IsAnswerVerified &&
                   !string.IsNullOrEmpty(NewPassword) &&
                   !string.IsNullOrEmpty(ConfirmNewPassword) &&
                   NewPassword.Length >= 6 &&
                   NewPassword == ConfirmNewPassword;
        }

        private void ResetPassword()
        {
            if (NewPassword.Length < 6)
            {
                ErrorMessage = "Пароль должен содержать минимум 6 символов";
                return;
            }

            if (NewPassword != ConfirmNewPassword)
            {
                ErrorMessage = "Пароли не совпадают";
                return;
            }

            try
            {
                // Сохраняем новый пароль
                var newPasswordHash = _passwordService.HashPassword(NewPassword);
                _settingsService.UpdatePassword(newPasswordHash);

                _dialogService.ShowInfo("Пароль успешно изменен!");

                // Закрываем окно восстановления и разблокируем приложение
                OnPasswordReset?.Invoke();
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Ошибка: {ex.Message}";
            }
        }

        private void Cancel()
        {
            OnCancelled?.Invoke();
        }

        #endregion

        #region События

        /// <summary>
        /// Событие успешного сброса пароля
        /// </summary>
        public System.Action OnPasswordReset { get; set; }

        /// <summary>
        /// Событие отмены
        /// </summary>
        public System.Action OnCancelled { get; set; }

        #endregion
    }
}