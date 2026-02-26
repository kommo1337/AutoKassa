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
        }

        private void OnToastRequested(object sender, ToastItem item)
        {
            Dispatcher.Invoke(() => ToastOverlay.ShowToast(item));
        }
    }
}
