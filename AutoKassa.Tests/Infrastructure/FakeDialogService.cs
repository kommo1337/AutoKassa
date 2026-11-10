using AutoKassa.Helpers;
using AutoKassa.Services;
using AutoKassa.ViewModels;

namespace AutoKassa.Tests.Infrastructure
{
    /// <summary>
    /// Фейк IDialogService для тестов: записывает показанные сообщения,
    /// диалоги не отображает.
    /// </summary>
    public class FakeDialogService : IDialogService
    {
        public string? LastError { get; private set; }
        public string? LastWarning { get; private set; }
        public string? LastInfo { get; private set; }

        /// <summary>Результат, возвращаемый ShowConfirmation.</summary>
        public bool ConfirmationResult { get; set; }

        /// <summary>Результат, возвращаемый ShowDialog.</summary>
        public bool? DialogResult { get; set; }

        /// <summary>Тип последнего запрошенного диалога ShowDialog.</summary>
        public Type? LastDialogViewModelType { get; private set; }

        /// <summary>Вызывается перед возвратом результата ShowDialog (эмуляция действий диалога).</summary>
        public Action? OnShowDialog { get; set; }

        /// <summary>Результат, возвращаемый ShowLockScreen.</summary>
        public bool? LockScreenResult { get; set; }

        /// <summary>Последнее сообщение, показанное через ShowConfirmation.</summary>
        public string? LastConfirmation { get; private set; }

        public void ShowError(string message, string title = "Ошибка") => LastError = message;

        public void ShowWarning(string message, string title = "Предупреждение") => LastWarning = message;

        public void ShowInfo(string message, string title = "Информация") => LastInfo = message;

        public bool ShowConfirmation(string message, string title = "Подтверждение")
        {
            LastConfirmation = message;
            return ConfirmationResult;
        }

        public bool? ShowDialog<TViewModel>() where TViewModel : ViewModelBase
        {
            LastDialogViewModelType = typeof(TViewModel);
            OnShowDialog?.Invoke();
            return DialogResult;
        }

        public bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase
        {
            LastDialogViewModelType = typeof(TViewModel);
            OnShowDialog?.Invoke();
            return DialogResult;
        }

        public bool? ShowLockScreen(LockScreenViewModel viewModel) => LockScreenResult;
    }
}
