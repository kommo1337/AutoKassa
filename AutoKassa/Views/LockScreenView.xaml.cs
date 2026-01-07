using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using AutoKassa.ViewModels;

namespace AutoKassa.Views
{
    /// <summary>
    /// Экран блокировки приложения
    /// </summary>
    public partial class LockScreenView : Window
    {
        private readonly LockScreenViewModel _viewModel;

        public LockScreenView(LockScreenViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            // Подписка на события
            _viewModel.OnUnlocked = OnUnlocked;
            _viewModel.OnPasswordError = OnPasswordError;

            // Фокус на поле пароля при загрузке
            Loaded += (s, e) => PasswordBox.Focus();
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

        /// <summary>
        /// Обработчик кнопки закрытия приложения
        /// </summary>
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