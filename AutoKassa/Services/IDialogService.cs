namespace AutoKassa.Services
{
    /// <summary>
    /// Интерфейс сервиса для отображения диалоговых окон
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Показать сообщение об ошибке
        /// </summary>
        void ShowError(string message, string title = "Ошибка");

        /// <summary>
        /// Показать предупреждение
        /// </summary>
        void ShowWarning(string message, string title = "Предупреждение");

        /// <summary>
        /// Показать информационное сообщение
        /// </summary>
        void ShowInfo(string message, string title = "Информация");

        /// <summary>
        /// Показать диалог подтверждения
        /// </summary>
        /// <returns>True если пользователь нажал "Да", иначе False</returns>
        bool ShowConfirmation(string message, string title = "Подтверждение");
    }
}