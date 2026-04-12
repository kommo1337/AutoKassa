using AutoKassa.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AutoKassa.Views
{
    public partial class ToastOverlayView : UserControl
    {
        private readonly DispatcherTimer _timer;
        private Action _currentUndoAction;
        private Action _currentActionCallback;
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
            _currentActionCallback = item.ActionCallback;
            MessageText.Text = item.Message;
            UndoButton.Visibility = item.HasUndo ? Visibility.Visible : Visibility.Collapsed;

            // Кнопка действия
            if (item.HasAction)
            {
                ActionButton.Content = item.ActionText ?? "Открыть";
                ActionButton.Visibility = Visibility.Visible;
            }
            else
            {
                ActionButton.Visibility = Visibility.Collapsed;
            }

            // Стиль в зависимости от типа
            ApplyStyle(item.Type);

            _remainingTicks = TotalTicks;
            ProgressScale.ScaleX = 1.0;
            ToastBorder.Visibility = Visibility.Visible;
            _timer.Start();
        }

        private void ApplyStyle(ToastType type)
        {
            // Скрыть все иконки
            IconDelete.Visibility = Visibility.Collapsed;
            IconSuccess.Visibility = Visibility.Collapsed;
            IconError.Visibility = Visibility.Collapsed;
            IconInfo.Visibility = Visibility.Collapsed;

            switch (type)
            {
                case ToastType.Delete:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xf2, 0xf2));
                    ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xfe, 0xca, 0xca));
                    ProgressBackground.Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xca, 0xca));
                    ProgressFill.Background = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
                    IconDelete.Visibility = Visibility.Visible;
                    break;

                case ToastType.Success:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(0xf0, 0xfd, 0xf4));
                    ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xbb, 0xf7, 0xd0));
                    ProgressBackground.Background = new SolidColorBrush(Color.FromRgb(0xbb, 0xf7, 0xd0));
                    ProgressFill.Background = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
                    IconSuccess.Visibility = Visibility.Visible;
                    break;

                case ToastType.Error:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xf2, 0xf2));
                    ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xfe, 0xca, 0xca));
                    ProgressBackground.Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xca, 0xca));
                    ProgressFill.Background = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
                    IconError.Visibility = Visibility.Visible;
                    break;

                case ToastType.Info:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(0xef, 0xf6, 0xff));
                    ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xbd, 0xdb, 0xfe));
                    ProgressBackground.Background = new SolidColorBrush(Color.FromRgb(0xbd, 0xdb, 0xfe));
                    ProgressFill.Background = new SolidColorBrush(Color.FromRgb(0x3b, 0x82, 0xf6));
                    IconInfo.Visibility = Visibility.Visible;
                    break;
            }
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
            _currentActionCallback = null;
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            var undo = _currentUndoAction;
            HideToast();
            undo?.Invoke();
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            var action = _currentActionCallback;
            HideToast();
            action?.Invoke();
        }
    }
}
