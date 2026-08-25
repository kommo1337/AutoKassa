using AutoKassa.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace AutoKassa.Views
{
    /// <summary>
    /// Экран блокировки приложения
    /// </summary>
    public partial class LockScreenView : Window
    {
        private readonly LockScreenViewModel _viewModel;
        private readonly RoutedEventHandler _loadedHandler;

        public LockScreenView(LockScreenViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            // Подписка на события
            _viewModel.OnUnlocked = OnUnlocked;
            _viewModel.OnPasswordError = OnPasswordError;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Фокус на поле пароля при загрузке
            _loadedHandler = (s, e) => PasswordBox.Focus();
            Loaded += _loadedHandler;
            Closed += OnClosed;
        }

        /// <summary>
        /// Обработчик изменения пароля
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.Password = ((PasswordBox)sender).Password;
        }

        /// <summary>
        /// Обработчик нажатия Enter в поле пароля
        /// </summary>
        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.UnlockCommand.CanExecute(null))
            {
                _viewModel.UnlockCommand.Execute(null);
            }
        }

        /// <summary>
        /// Переключение панелей входа/восстановления:
        /// очистка полей и установка фокуса (чистая UI-логика)
        /// </summary>
        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LockScreenViewModel.IsPasswordRecoveryMode))
                return;

            if (_viewModel.IsPasswordRecoveryMode)
            {
                // Очищаем поля восстановления и фокусируемся на ответе
                NewPasswordBox.Clear();
                ConfirmNewPasswordBox.Clear();
                Dispatcher.BeginInvoke(new System.Action(() => AnswerTextBox.Focus()),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else
            {
                // Вернулись на панель входа
                PasswordBox.Clear();
                Dispatcher.BeginInvoke(new System.Action(() => PasswordBox.Focus()),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// Обработчик изменения нового пароля (восстановление)
        /// </summary>
        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.Recovery.NewPassword = ((PasswordBox)sender).Password;
        }

        /// <summary>
        /// Обработчик изменения подтверждения нового пароля (восстановление)
        /// </summary>
        private void ConfirmNewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.Recovery.ConfirmNewPassword = ((PasswordBox)sender).Password;
        }

        /// <summary>
        /// Enter в полях восстановления: проверка ответа или смена пароля
        /// </summary>
        private void RecoveryInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (_viewModel.Recovery.IsAnswerVerified)
            {
                if (_viewModel.Recovery.ResetPasswordCommand.CanExecute(null))
                    _viewModel.Recovery.ResetPasswordCommand.Execute(null);
            }
            else if (_viewModel.Recovery.VerifyAnswerCommand.CanExecute(null))
            {
                _viewModel.Recovery.VerifyAnswerCommand.Execute(null);
            }
        }

        /// <summary>
        /// Успешная разблокировка
        /// </summary>
        private void OnUnlocked()
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Ошибка пароля - анимация тряски
        /// </summary>
        private void OnPasswordError()
        {
            // Очищаем поле пароля
            PasswordBox.Clear();
            PasswordBox.Focus();

            // Анимация тряски
            var shakeAnimation = new DoubleAnimation
            {
                From = 0,
                To = 10,
                Duration = System.TimeSpan.FromMilliseconds(50),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };

            var transform = new System.Windows.Media.TranslateTransform();
            PasswordBorder.RenderTransform = transform;
            transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, shakeAnimation);
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.OnUnlocked = null;
            _viewModel.OnPasswordError = null;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            Loaded -= _loadedHandler;
            Closed -= OnClosed;
            _viewModel?.Dispose();
        }

        /// <summary>
        /// Обработчик кнопки закрытия приложения
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы действительно хотите закрыть приложение?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                // Закрываем окно блокировки без результата
                this.Close();

                // Закрываем приложение
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Перетаскивание окна при клике на фон (опционально)
        /// </summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                // Не перетаскиваем если кликнули на карточку
                if (e.OriginalSource is FrameworkElement element)
                {
                    var card = FindVisualParent<Border>(element);
                    if (card?.Background == System.Windows.Media.Brushes.White)
                        return;
                }

                this.DragMove();
            }
        }

        /// <summary>
        /// Поиск родительского элемента
        /// </summary>
        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }
    }
}
