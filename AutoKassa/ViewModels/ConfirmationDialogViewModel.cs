using AutoKassa.Helpers;
using System;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для кастомного диалога подтверждения
    /// </summary>
    public class ConfirmationDialogViewModel : ViewModelBase
    {
        public string Title { get; }
        public string Message { get; }
        public string ConfirmText { get; }
        public string CancelText { get; }
        public bool IsDestructive { get; }

        public bool Result { get; private set; }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action? RequestClose;

        public ConfirmationDialogViewModel(
            string title,
            string message,
            string confirmText = "Да",
            string cancelText = "Нет",
            bool isDestructive = false)
        {
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
            IsDestructive = isDestructive;

            ConfirmCommand = new RelayCommand(_ => { Result = true; RequestClose?.Invoke(); });
            CancelCommand = new RelayCommand(_ => { Result = false; RequestClose?.Invoke(); });
        }
    }
}
