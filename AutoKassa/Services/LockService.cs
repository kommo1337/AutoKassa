using System;
using System.Windows;
using System.Windows.Threading;
using AutoKassa.Views;
using AutoKassa.ViewModels;
using AutoKassa.Helpers;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис блокировки приложения
    /// </summary>
    public class LockService : ILockService
    {
        private readonly IPasswordService _passwordService;
        private readonly ISettingsService _settingsService;
        private DispatcherTimer _autoLockTimer;
        private bool _isLocked;

        public LockService(
            IPasswordService passwordService,
            ISettingsService settingsService)
        {
            _passwordService = passwordService;
            _settingsService = settingsService;
        }

        /// <summary>
        /// Проверить, заблокировано ли приложение
        /// </summary>
        public bool IsLocked => _isLocked;

        /// <summary>
        /// Заблокировать приложение
        /// </summary>
        public void Lock()
        {
            if (_isLocked) return;

            _isLocked = true;

            // Получаем главное окно
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                _isLocked = false;
                return;
            }

            // Применяем размытие к главному окну
            BlurHelper.ApplyBlur(mainWindow, radius: 15);

            // Создаем ViewModel для экрана блокировки
            var viewModel = new LockScreenViewModel(_passwordService, _settingsService);

            // Создаем и показываем окно блокировки
            var lockWindow = new LockScreenView(viewModel)
            {
                Owner = mainWindow,
                Width = mainWindow.ActualWidth,
                Height = mainWindow.ActualHeight
            };

            // Подписываемся на закрытие окна
            lockWindow.Closed += (s, e) =>
            {
                // Убираем размытие в любом случае
                BlurHelper.RemoveBlur(mainWindow);
                _isLocked = false;
            };

            var result = lockWindow.ShowDialog();

            if (result == true)
            {
                // Успешная разблокировка
                _isLocked = false;
                ResetAutoLockTimer();
            }
            // Если result == false, это значит окно закрыто через крестик
            // и Application.Shutdown() уже вызван в LockScreenView.xaml.cs
        }

        /// <summary>
        /// Запустить таймер автоблокировки
        /// </summary>
        public void StartAutoLockTimer()
        {
            var timeout = _settingsService.GetAutoLockTimeout();

            if (timeout <= 0) return; // Автоблокировка отключена

            if (_autoLockTimer == null)
            {
                _autoLockTimer = new DispatcherTimer();
                _autoLockTimer.Tick += AutoLockTimer_Tick;
            }

            _autoLockTimer.Interval = TimeSpan.FromMinutes(timeout);
            _autoLockTimer.Start();
        }

        /// <summary>
        /// Остановить таймер автоблокировки
        /// </summary>
        public void StopAutoLockTimer()
        {
            _autoLockTimer?.Stop();
        }

        /// <summary>
        /// Сбросить таймер автоблокировки
        /// </summary>
        public void ResetAutoLockTimer()
        {
            if (_autoLockTimer != null && _autoLockTimer.IsEnabled)
            {
                _autoLockTimer.Stop();
                StartAutoLockTimer();
            }
        }

        /// <summary>
        /// Обработчик тика таймера автоблокировки
        /// </summary>
        private void AutoLockTimer_Tick(object sender, EventArgs e)
        {
            _autoLockTimer.Stop();
            Lock();
        }
    }
}