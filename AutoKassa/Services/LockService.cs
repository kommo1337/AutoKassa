using AutoKassa.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис блокировки приложения
    /// </summary>
    public class LockService : ILockService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialogService;
        private readonly IPasswordService _passwordService;
        private readonly ISettingsService _settingsService;
        private DispatcherTimer _autoLockTimer;
        private bool _isLocked;

        public LockService(
            IServiceProvider serviceProvider,
            IDialogService dialogService,
            IPasswordService passwordService,
            ISettingsService settingsService)
        {
            _serviceProvider = serviceProvider;
            _dialogService = dialogService;
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

            // Создаем ViewModel для экрана блокировки через DI
            var viewModel = _serviceProvider.GetRequiredService<LockScreenViewModel>();

            var result = _dialogService.ShowLockScreen(viewModel);

            if (result == true)
            {
                // Успешная разблокировка
                _isLocked = false;
                ResetAutoLockTimer();
            }
            else
            {
                // Окно закрыто без разблокировки (крестик) — сбрасываем флаг
                _isLocked = false;
            }
        }

        /// <summary>
        /// Запустить таймер автоблокировки
        /// </summary>
        public void StartAutoLockTimer()
        {
            if (!_settingsService.GetSettings().AutoLockEnabled) return; // Автоблокировка отключена в настройках

            var timeout = _settingsService.GetAutoLockTimeout();

            if (timeout <= 0) return; // Автоблокировка отключена (таймаут 0)

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

        /// <summary>
        /// Освободить ресурсы сервиса блокировки
        /// </summary>
        public void Dispose()
        {
            if (_autoLockTimer != null)
            {
                _autoLockTimer.Tick -= AutoLockTimer_Tick;
                _autoLockTimer.Stop();
                _autoLockTimer = null;
            }
        }
    }
}