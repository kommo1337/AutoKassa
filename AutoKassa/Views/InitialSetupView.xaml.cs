using AutoKassa.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AutoKassa.Views
{
    /// <summary>
    /// Окно первоначальной настройки пароля
    /// </summary>
    public partial class InitialSetupView : Window
    {
        private readonly InitialSetupViewModel _viewModel;

        public InitialSetupView(InitialSetupViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            // Подписка на событие завершения настройки
            _viewModel.OnSetupCompleted = OnSetupCompleted;
        }

        /// <summary>
        /// Обработчик изменения пароля (PasswordBox не поддерживает Binding)
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.Password = ((PasswordBox)sender).Password;
        }

        /// <summary>
        /// Обработчик изменения подтверждения пароля
        /// </summary>
        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.ConfirmPassword = ((PasswordBox)sender).Password;
        }

        /// <summary>
        /// Обработчик завершения настройки
        /// </summary>
        private void OnSetupCompleted()
        {
            DialogResult = true;
            Close();
        }
    }
}