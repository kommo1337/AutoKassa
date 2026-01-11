using AutoKassa.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AutoKassa.Views
{
    /// <summary>
    /// Логика взаимодействия для ChangePasswordView.xaml
    /// </summary>
    public partial class ChangePasswordView : Window
    {
        private readonly ChangePasswordViewModel _viewModel;

        public ChangePasswordView(ChangePasswordViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;

            _viewModel.OnClose = (result) =>
            {
                DialogResult = result;
                Close();
            };
        }

        private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.CurrentPassword = passwordBox.Password;
            }
        }

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.NewPassword = passwordBox.Password;
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.ConfirmPassword = passwordBox.Password;
            }
        }
    }
}
