using System;

namespace AutoKassa.Services
{
    /// <summary>
    /// Интерфейс сервиса для отображения всплывающих уведомлений (toast)
    /// </summary>
    public interface IToastNotificationService
    {
        /// <summary>
        /// Событие запроса показа тоста
        /// </summary>
        event EventHandler<ToastItem> ToastRequested;

        /// <summary>
        /// Показать уведомление об успехе
        /// </summary>
        void ShowSuccess(string message);

        /// <summary>
        /// Показать уведомление об ошибке
        /// </summary>
        void ShowError(string message);

        /// <summary>
        /// Показать информационное уведомление
        /// </summary>
        void ShowInfo(string message);

        /// <summary>
        /// Показать уведомление об удалении с кнопкой «Отменить»
        /// </summary>
        void ShowDeleteWithUndo(string message, Action undoAction);

        /// <summary>
        /// Показать уведомление с кнопкой действия (например «Открыть»)
        /// </summary>
        void ShowWithAction(string message, string actionText, Action action, ToastType type = ToastType.Success);
    }
}
