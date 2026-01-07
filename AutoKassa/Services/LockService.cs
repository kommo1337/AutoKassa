using System;
using System.Windows;
using System.Windows.Threading;
using AutoKassa.Views;
using AutoKassa.ViewModels;

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

            // Создаем ViewModel для экрана блокировки
            var viewModel = new LockScreenViewModel(_passwordService, _settingsService);

            // Создаем и показываем окно блокировки
            var lockWindow = new LockScreenView(viewModel);
            var result = lockWindow.ShowDialog();

            if (result == true)
            {
                _isLocked = false;
                // Сбрасываем таймер автоблокировки
                ResetAutoLockTimer();
            }
            else
            {
                // Если пользователь закрыл окно без разблокировки - закрываем приложение
                Application.Current.Shutdown();
            }
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