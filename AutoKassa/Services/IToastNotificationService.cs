namespace AutoKassa.Services
{
    /// <summary>
    /// Интерфейс сервиса для отображения всплывающих уведомлений (toast)
    /// </summary>
    public interface IToastNotificationService
    {
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
    }
}