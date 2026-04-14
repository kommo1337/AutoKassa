using AutoKassa.Services;
using AutoKassa.ViewModels;
using System.Windows;

namespace AutoKassa
{
    /// <summary>
    /// Главное окно приложения
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IToastNotificationService _toastService;

        public MainWindow(MainWindowViewModel viewModel, IToastNotificationService toastService)
        {
            InitializeComponent();
            DataContext = viewModel;

            _toastService = toastService;
            _toastService.ToastRequested += OnToastRequested;

            StateChanged += OnWindowStateChanged;
        }

        private void OnWindowStateChanged(object sender, System.EventArgs e)
        {
            // При WindowStyle=None в Maximized окно «вылезает» за экран — компенсируем отступом.
            BorderThickness = WindowState == WindowState.Maximized
                ? new Thickness(7)
                : new Thickness(0);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnToastRequested(object sender, ToastItem item)
        {
            Dispatcher.Invoke(() => ToastOverlay.ShowToast(item));
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _toastService.ToastRequested -= OnToastRequested;
            (DataContext as System.IDisposable)?.Dispose();
            base.OnClosed(e);
        }
    }
}
