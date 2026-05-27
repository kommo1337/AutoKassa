using AutoKassa.Services;
using AutoKassa.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                mmi.ptMaxPosition.x = (int)SystemParameters.WorkArea.Left;
                mmi.ptMaxPosition.y = (int)SystemParameters.WorkArea.Top;
                mmi.ptMaxSize.x = (int)SystemParameters.WorkArea.Width;
                mmi.ptMaxSize.y = (int)SystemParameters.WorkArea.Height;
                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }
    }
}
