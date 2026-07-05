using AutoKassa.Helpers;
using AutoKassa.ViewModels;

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

        /// <summary>
        /// Показать диалог для указанного ViewModel.
        /// ViewModel создаётся через DI.
        /// </summary>
        /// <typeparam name="TViewModel">Тип ViewModel диалога</typeparam>
        /// <returns>Результат диалога или null</returns>
        bool? ShowDialog<TViewModel>() where TViewModel : ViewModelBase;

        /// <summary>
        /// Показать диалог для переданного ViewModel.
        /// </summary>
        /// <typeparam name="TViewModel">Тип ViewModel диалога</typeparam>
        /// <param name="viewModel">Экземпляр ViewModel</param>
        /// <returns>Результат диалога или null</returns>
        bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase;

        /// <summary>
        /// Показать экран блокировки приложения
        /// </summary>
        /// <param name="viewModel">ViewModel экрана блокировки</param>
        /// <returns>True при успешной разблокировке, иначе null/false</returns>
        bool? ShowLockScreen(LockScreenViewModel viewModel);
    }
}
