using System.Windows;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для отображения стандартных диалоговых окон
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <summary>
        /// Показать сообщение об ошибке
        /// </summary>
        public void ShowError(string message, string title = "Ошибка")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Показать предупреждение
        /// </summary>
        public void ShowWarning(string message, string title = "Предупреждение")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Показать информационное сообщение
        /// </summary>
        public void ShowInfo(string message, string title = "Информация")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Показать диалог подтверждения
        /// </summary>
        public bool ShowConfirmation(string message, string title = "Подтверждение")
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
    }
}