using AutoKassa.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AutoKassa.Views
{
    public partial class ToastOverlayView : UserControl
    {
        private readonly DispatcherTimer _timer;
        private Action _currentUndoAction;
        private int _remainingTicks;

        private const int TotalTicks = 100;
        private const int TickIntervalMs = 50; // 100 × 50ms = 5 секунд

        public ToastOverlayView()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickIntervalMs) };
            _timer.Tick += OnTick;

            Unloaded += (_, _) =>
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
            };
        }

        public void ShowToast(ToastItem item)
        {
            _timer.Stop();
            _currentUndoAction = item.UndoAction;
            MessageText.Text = item.Message;
            UndoButton.Visibility = item.HasUndo ? Visibility.Visible : Visibility.Collapsed;

            _remainingTicks = TotalTicks;
            ProgressScale.ScaleX = 1.0;
            ToastBorder.Visibility = Visibility.Visible;
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            _remainingTicks--;
            double fraction = Math.Max(0.0, (double)_remainingTicks / TotalTicks);
            ProgressScale.ScaleX = fraction;

            if (_remainingTicks <= 0)
                HideToast();
        }

        private void HideToast()
        {
            _timer.Stop();
            ToastBorder.Visibility = Visibility.Collapsed;
            _currentUndoAction = null;
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            var undo = _currentUndoAction;
            HideToast();
            undo?.Invoke();
        }
    }
}
