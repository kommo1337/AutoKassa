using AutoKassa.Helpers;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для экрана первоначальной настройки пароля
    /// </summary>
    public class InitialSetupViewModel : ViewModelBase
    {
        private readonly IPasswordService _passwordService;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private string _password;
        private string _confirmPassword;
        private SecurityQuestionItem _selectedQuestion;
        private string _customQuestion;
        private string _answer;

        public InitialSetupViewModel(
            IPasswordService passwordService,
            ISettingsService settingsService,
            IDialogService dialogService)
        {
            _passwordService = passwordService;
            _settingsService = settingsService;
            _dialogService = dialogService;

            // Загрузка списка вопросов
            SecurityQuestions = SecurityQuestionHelper.GetQuestionsList();
            SelectedQuestion = SecurityQuestions.First();

            // Команды
            ContinueCommand = new AsyncRelayCommand(ContinueAsync, CanContinue);
        }

        #region Свойства

        /// <summary>
        /// Список секретных вопросов
        /// </summary>
        public List<SecurityQuestionItem> SecurityQuestions { get; }

        /// <summary>
        /// Пароль
        /// </summary>
        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    ValidatePassword();
                    OnPropertyChanged(nameof(PasswordStrength));
                    OnPropertyChanged(nameof(PasswordStrengthText));
                }
            }
        }

        /// <summary>
        /// Подтверждение пароля
        /// </summary>
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

        /// <summary>
        /// Выбранный секретный вопрос
        /// </summary>
        public SecurityQuestionItem SelectedQuestion
        {
            get => _selectedQuestion;
            set
            {
                if (SetProperty(ref _selectedQuestion, value))
                {
                    OnPropertyChanged(nameof(IsCustomQuestion));
                    ValidateCustomQuestion();
                }
            }
        }

        /// <summary>
        /// Свой секретный вопрос (если выбран Custom)
        /// </summary>
        public string CustomQuestion
        {
            get => _customQuestion;
            set
            {
                if (SetProperty(ref _customQuestion, value))
                {
                    ValidateCustomQuestion();
                }
            }
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
                    ValidateAnswer();
                }
            }
        }

        /// <summary>
        /// Показывать ли поле для своего вопроса
        /// </summary>
        public bool IsCustomQuestion => SelectedQuestion?.Question == SecurityQuestion.Custom;

        /// <summary>
        /// Надежность пароля (0-3: слабый, средний, сильный)
        /// </summary>
        public int PasswordStrength
        {
            get
            {
                if (string.IsNullOrEmpty(Password)) return 0;
                if (Password.Length < 6) return 0;

                int strength = 1; // базовая надежность

                if (Password.Length >= 8) strength++;
                if (Password.Any(char.IsDigit)) strength++;
                if (Password.Any(ch => !char.IsLetterOrDigit(ch))) strength++;

                return strength > 3 ? 3 : strength;
            }
        }

        /// <summary>
        /// Текст надежности пароля
        /// </summary>
        public string PasswordStrengthText
        {
            get
            {
                return PasswordStrength switch
                {
                    0 => "Слабый",
                    1 => "Слабый",
                    2 => "Средний",
                    3 => "Сильный",
                    _ => ""
                };
            }
        }

        #endregion

        #region Ошибки валидации

        // Ошибки хранятся в INotifyDataErrorInfo (ViewModelBase),
        // свойства — computed для привязки в XAML
        public string PasswordError => GetFirstError(nameof(Password));
        public string ConfirmPasswordError => GetFirstError(nameof(ConfirmPassword));
        public string AnswerError => GetFirstError(nameof(Answer));
        public string CustomQuestionError => GetFirstError(nameof(CustomQuestion));

        #endregion

        #region Команды

        public ICommand ContinueCommand { get; }

        #endregion

        #region Методы валидации

        private void ValidatePassword()
        {
            if (string.IsNullOrEmpty(Password))
            {
                SetErrors(nameof(Password), new[] { "Введите пароль" });
            }
            else if (Password.Length < 6)
            {
                SetErrors(nameof(Password), new[] { "Пароль должен содержать минимум 6 символов" });
            }
            else
            {
                ClearErrors(nameof(Password));
            }
            OnPropertyChanged(nameof(PasswordError));

            // Перепроверить подтверждение пароля
            if (!string.IsNullOrEmpty(ConfirmPassword))
            {
                ValidateConfirmPassword();
            }
        }

        private void ValidateConfirmPassword()
        {
            if (string.IsNullOrEmpty(ConfirmPassword))
            {
                SetErrors(nameof(ConfirmPassword), new[] { "Подтвердите пароль" });
            }
            else if (Password != ConfirmPassword)
            {
                SetErrors(nameof(ConfirmPassword), new[] { "Пароли не совпадают" });
            }
            else
            {
                ClearErrors(nameof(ConfirmPassword));
            }
            OnPropertyChanged(nameof(ConfirmPasswordError));
        }

        private void ValidateAnswer()
        {
            if (string.IsNullOrEmpty(Answer))
            {
                SetErrors(nameof(Answer), new[] { "Введите ответ на секретный вопрос" });
            }
            else if (Answer.Length < 2)
            {
                SetErrors(nameof(Answer), new[] { "Ответ слишком короткий" });
            }
            else
            {
                ClearErrors(nameof(Answer));
            }
            OnPropertyChanged(nameof(AnswerError));
        }

        private void ValidateCustomQuestion()
        {
            const int MaxCustomQuestionLength = 200;

            if (IsCustomQuestion)
            {
                if (string.IsNullOrEmpty(CustomQuestion))
                {
                    SetErrors(nameof(CustomQuestion), new[] { "Введите свой вопрос" });
                }
                else if (CustomQuestion.Length < 5)
                {
                    SetErrors(nameof(CustomQuestion), new[] { "Вопрос слишком короткий" });
                }
                else if (CustomQuestion.Length > MaxCustomQuestionLength)
                {
                    SetErrors(nameof(CustomQuestion), new[] { $"Свой вопрос не должен превышать {MaxCustomQuestionLength} символов" });
                }
                else
                {
                    ClearErrors(nameof(CustomQuestion));
                }
            }
            else
            {
                ClearErrors(nameof(CustomQuestion));
            }
            OnPropertyChanged(nameof(CustomQuestionError));
        }

        private bool CanContinue()
        {
            return !HasErrors &&
                   !string.IsNullOrEmpty(Password) &&
                   !string.IsNullOrEmpty(ConfirmPassword) &&
                   !string.IsNullOrEmpty(Answer) &&
                   Password == ConfirmPassword &&
                   (!IsCustomQuestion || !string.IsNullOrEmpty(CustomQuestion));
        }

        #endregion

        #region Логика настройки

        private async Task ContinueAsync()
        {
            // Валидация всех полей
            ValidatePassword();
            ValidateConfirmPassword();
            ValidateAnswer();
            ValidateCustomQuestion();

            if (!CanContinue())
            {
                _dialogService.ShowError("Пожалуйста, исправьте ошибки в форме");
                return;
            }

            try
            {
                // Хешируем пароль и ответ
                var passwordHash = _passwordService.HashPassword(Password);
                var answerHash = _passwordService.HashPassword(Answer.Trim().ToLower()); // Приводим к нижнему регистру для проверки

                // Сохраняем в настройки
                await _settingsService.SetPasswordAsync(
                    passwordHash,
                    SelectedQuestion.Question,
                    answerHash,
                    IsCustomQuestion ? CustomQuestion : null
                );

                // Закрываем окно настройки и открываем главное окно
                OnSetupCompleted?.Invoke();
            }
            catch (System.Exception ex)
            {
                _dialogService.ShowError($"Ошибка при сохранении настроек: {ex.Message}");
            }
        }

        #endregion

        #region События

        /// <summary>
        /// Событие завершения настройки
        /// </summary>
        public System.Action OnSetupCompleted { get; set; }

        #endregion
    }
}
