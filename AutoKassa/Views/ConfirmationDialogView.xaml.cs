using AutoKassa.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace AutoKassa.Views
{
    /// <summary>
    /// Кастомное модальное окно подтверждения в стиле приложения
    /// </summary>
    public partial class ConfirmationDialogView : Window
    {
        private readonly ConfirmationDialogViewModel _viewModel;

        public ConfirmationDialogView(ConfirmationDialogViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.RequestClose += () => Close();

            Loaded += (_, _) => ConfirmButton.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_viewModel.ConfirmCommand.CanExecute(null))
                    _viewModel.ConfirmCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_viewModel.CancelCommand.CanExecute(null))
                    _viewModel.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
