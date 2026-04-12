using System;

namespace AutoKassa.Services
{
    /// <summary>
    /// Реализация сервиса toast-уведомлений на основе событий
    /// </summary>
    public class ToastNotificationService : IToastNotificationService
    {
        public event EventHandler<ToastItem> ToastRequested;

        public void ShowSuccess(string message)
            => Raise(new ToastItem { Message = message, Type = ToastType.Success });

        public void ShowError(string message)
            => Raise(new ToastItem { Message = message, Type = ToastType.Error });

        public void ShowInfo(string message)
            => Raise(new ToastItem { Message = message, Type = ToastType.Info });

        public void ShowDeleteWithUndo(string message, Action undoAction)
            => Raise(new ToastItem { Message = message, Type = ToastType.Delete, UndoAction = undoAction });

        public void ShowWithAction(string message, string actionText, Action action, ToastType type = ToastType.Success)
            => Raise(new ToastItem { Message = message, Type = type, ActionCallback = action, ActionText = actionText });

        private void Raise(ToastItem item)
            => ToastRequested?.Invoke(this, item);
    }
}
