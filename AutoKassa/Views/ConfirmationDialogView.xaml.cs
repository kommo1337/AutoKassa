using AutoKassa.ViewModels;
using System;
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
        private readonly Action _requestCloseHandler;
        private readonly RoutedEventHandler _loadedHandler;

        public ConfirmationDialogView(ConfirmationDialogViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            _requestCloseHandler = () => Close();
            _loadedHandler = (_, _) => ConfirmButton.Focus();

            _viewModel.RequestClose += _requestCloseHandler;
            Loaded += _loadedHandler;
            Unloaded += OnUnloaded;
            Closed += OnClosed;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.RequestClose -= _requestCloseHandler;
            Loaded -= _loadedHandler;
            Unloaded -= OnUnloaded;
            Closed -= OnClosed;
            _viewModel?.Dispose();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.RequestClose -= _requestCloseHandler;
            Loaded -= _loadedHandler;
            Unloaded -= OnUnloaded;
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
