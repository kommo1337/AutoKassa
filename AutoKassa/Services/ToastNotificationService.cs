using System.Windows;

namespace AutoKassa.Services
{
    /// <summary>
    /// Базовая реализация сервиса toast-уведомлений
    /// (Позже заменим на более красивую реализацию с анимациями)
    /// </summary>
    public class ToastNotificationService : IToastNotificationService
    {
        private readonly IDialogService _dialogService;

        public ToastNotificationService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        /// <summary>
        /// Показать уведомление об успехе
        /// </summary>
        public void ShowSuccess(string message)
        {
            // TODO: Реализовать красивое toast-уведомление
            // Пока используем простой MessageBox
            _dialogService.ShowInfo(message, "Успешно");
        }

        /// <summary>
        /// Показать уведомление об ошибке
        /// </summary>
        public void ShowError(string message)
        {
            _dialogService.ShowError(message);
        }

        /// <summary>
        /// Показать информационное уведомление
        /// </summary>
        public void ShowInfo(string message)
        {
            _dialogService.ShowInfo(message);
        }
    }
}