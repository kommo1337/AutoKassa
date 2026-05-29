using AutoKassa.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AutoKassa.Views
{
    public partial class ToastOverlayView : UserControl
    {
        private readonly DispatcherTimer _timer;
        private readonly Storyboard _showStoryboard;
        private readonly Storyboard _hideStoryboard;

        private Action _currentUndoAction;
        private Action _currentActionCallback;
        private int _remainingTicks;

        private const int TotalTicks = 50;
        private const int TickIntervalMs = 35; // 50 × 35ms ≈ 1.75 секунды плавного обновления

        // Кэшированные кисти — избегаем создания SolidColorBrush при каждом ShowToast
        private static readonly SolidColorBrush DeleteBackground  = new(Color.FromRgb(0xfe, 0xf2, 0xf2));
        private static readonly SolidColorBrush DeleteBorder      = new(Color.FromRgb(0xfe, 0xca, 0xca));
        private static readonly SolidColorBrush DeleteProgressBg  = new(Color.FromRgb(0xfe, 0xca, 0xca));
        private static readonly SolidColorBrush DeleteProgressFill = new(Color.FromRgb(0xef, 0x44, 0x44));

        private static readonly SolidColorBrush SuccessBackground = new(Color.FromRgb(0xf0, 0xfd, 0xf4));
        private static readonly SolidColorBrush SuccessBorder     = new(Color.FromRgb(0xbb, 0xf7, 0xd0));
        private static readonly SolidColorBrush SuccessProgressBg = new(Color.FromRgb(0xbb, 0xf7, 0xd0));
        private static readonly SolidColorBrush SuccessProgressFill = new(Color.FromRgb(0x22, 0xc5, 0x5e));

        private static readonly SolidColorBrush ErrorBackground   = new(Color.FromRgb(0xfe, 0xf2, 0xf2));
        private static readonly SolidColorBrush ErrorBorder       = new(Color.FromRgb(0xfe, 0xca, 0xca));
        private static readonly SolidColorBrush ErrorProgressBg   = new(Color.FromRgb(0xfe, 0xca, 0xca));
        private static readonly SolidColorBrush ErrorProgressFill = new(Color.FromRgb(0xef, 0x44, 0x44));

        private static readonly SolidColorBrush InfoBackground    = new(Color.FromRgb(0xef, 0xf6, 0xff));
        private static readonly SolidColorBrush InfoBorder        = new(Color.FromRgb(0xbd, 0xdb, 0xfe));
        private static readonly SolidColorBrush InfoProgressBg    = new(Color.FromRgb(0xbd, 0xdb, 0xfe));
        private static readonly SolidColorBrush InfoProgressFill  = new(Color.FromRgb(0x3b, 0x82, 0xf6));

        public ToastOverlayView()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickIntervalMs) };
            _timer.Tick += OnTick;

            // Анимация появления (opacity + slide up)
            _showStoryboard = new Storyboard();
            var showOpacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(showOpacity, ToastBorder);
            Storyboard.SetTargetProperty(showOpacity, new PropertyPath(OpacityProperty));
            _showStoryboard.Children.Add(showOpacity);

            var showTranslate = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(showTranslate, ToastTranslate);
            Storyboard.SetTargetProperty(showTranslate, new PropertyPath(TranslateTransform.YProperty));
            _showStoryboard.Children.Add(showTranslate);

            // Анимация исчезновения (opacity + slide down)
            _hideStoryboard = new Storyboard();
            var hideOpacity = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(hideOpacity, ToastBorder);
            Storyboard.SetTargetProperty(hideOpacity, new PropertyPath(OpacityProperty));
            _hideStoryboard.Children.Add(hideOpacity);

            var hideTranslate = new DoubleAnimation(0, 20, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(hideTranslate, ToastTranslate);
            Storyboard.SetTargetProperty(hideTranslate, new PropertyPath(TranslateTransform.YProperty));
            _hideStoryboard.Children.Add(hideTranslate);

            _hideStoryboard.Completed += (_, _) =>
            {
                ToastBorder.Visibility = Visibility.Collapsed;
            };

            Unloaded += (_, _) =>
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
                _showStoryboard.Stop();
                _hideStoryboard.Stop();
            };
        }

        public void ShowToast(ToastItem item)
        {
            _timer.Stop();
            _showStoryboard.Stop();
            _hideStoryboard.Stop();

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

            // Сброс в начальное состояние и плавное появление
            ToastBorder.Visibility = Visibility.Visible;
            ToastBorder.Opacity = 0;
            ToastTranslate.Y = 20;

            _showStoryboard.Begin();
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
                    ToastBorder.Background = DeleteBackground;
                    ToastBorder.BorderBrush = DeleteBorder;
                    ProgressBackground.Background = DeleteProgressBg;
                    ProgressFill.Background = DeleteProgressFill;
                    IconDelete.Visibility = Visibility.Visible;
                    break;

                case ToastType.Success:
                    ToastBorder.Background = SuccessBackground;
                    ToastBorder.BorderBrush = SuccessBorder;
                    ProgressBackground.Background = SuccessProgressBg;
                    ProgressFill.Background = SuccessProgressFill;
                    IconSuccess.Visibility = Visibility.Visible;
                    break;

                case ToastType.Error:
                    ToastBorder.Background = ErrorBackground;
                    ToastBorder.BorderBrush = ErrorBorder;
                    ProgressBackground.Background = ErrorProgressBg;
                    ProgressFill.Background = ErrorProgressFill;
                    IconError.Visibility = Visibility.Visible;
                    break;

                case ToastType.Info:
                    ToastBorder.Background = InfoBackground;
                    ToastBorder.BorderBrush = InfoBorder;
                    ProgressBackground.Background = InfoProgressBg;
                    ProgressFill.Background = InfoProgressFill;
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
            _showStoryboard.Stop();

            if (ToastBorder.Visibility == Visibility.Visible)
            {
                _hideStoryboard.Begin();
            }

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
