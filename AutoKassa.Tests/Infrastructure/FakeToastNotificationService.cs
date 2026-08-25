using AutoKassa.Services;

namespace AutoKassa.Tests.Infrastructure
{
    /// <summary>
    /// Фейк IToastNotificationService для тестов: записывает показанные уведомления,
    /// UI не затрагивает.
    /// </summary>
    public class FakeToastNotificationService : IToastNotificationService
    {
        public string? LastSuccess { get; private set; }
        public string? LastError { get; private set; }
        public string? LastInfo { get; private set; }

        public event EventHandler<ToastItem>? ToastRequested;

        public void ShowSuccess(string message) => LastSuccess = message;

        public void ShowError(string message) => LastError = message;

        public void ShowInfo(string message) => LastInfo = message;

        public void ShowDeleteWithUndo(string message, Action undoAction) { }

        public void ShowWithAction(string message, string actionText, Action action, ToastType type = ToastType.Success) { }
    }
}
