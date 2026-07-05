using AutoKassa.Helpers;
using AutoKassa.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для отображения диалоговых окон
    /// </summary>
    public class DialogService : IDialogService
    {
        private readonly IServiceProvider _serviceProvider;

        public DialogService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

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

        /// <summary>
        /// Показать диалог для указанного ViewModel, создавая его через DI
        /// </summary>
        public bool? ShowDialog<TViewModel>() where TViewModel : ViewModelBase
        {
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
            return ShowDialog(viewModel);
        }

        /// <summary>
        /// Показать диалог для переданного ViewModel
        /// </summary>
        public bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            var viewType = ResolveViewType(typeof(TViewModel));
            var window = CreateWindow(viewType, viewModel);
            return window.ShowDialog();
        }

        /// <summary>
        /// Показать экран блокировки приложения
        /// </summary>
        public bool? ShowLockScreen(LockScreenViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
                return null;

            BlurHelper.ApplyBlur(mainWindow, radius: 15);

            try
            {
                var viewType = ResolveViewType(typeof(LockScreenViewModel));
                var window = CreateWindow(viewType, viewModel);

                // Растягиваем окно блокировки на весь главное окно
                window.Width = mainWindow.ActualWidth;
                window.Height = mainWindow.ActualHeight;

                window.Closed += (s, e) =>
                {
                    BlurHelper.RemoveBlur(mainWindow);
                };

                return window.ShowDialog();
            }
            catch
            {
                BlurHelper.RemoveBlur(mainWindow);
                throw;
            }
        }

        /// <summary>
        /// Создаёт окно для указанного ViewModel по соглашению об именах
        /// </summary>
        private Window CreateWindow(Type viewType, ViewModelBase viewModel)
        {
            var window = (Window)ActivatorUtilities.CreateInstance(_serviceProvider, viewType, viewModel);
            window.Owner = Application.Current.MainWindow;
            return window;
        }

        /// <summary>
        /// Определяет тип View по типу ViewModel по соглашению:
        /// ViewModels.X.YViewModel -> Views.X.YView
        /// </summary>
        private static Type ResolveViewType(Type viewModelType)
        {
            var fullName = viewModelType.FullName;
            if (string.IsNullOrEmpty(fullName))
                throw new InvalidOperationException($"Не удалось определить View для {viewModelType.Name}: пустое полное имя типа");

            var viewName = fullName
                .Replace("ViewModels", "Views")
                .Replace("ViewModel", "View");

            var viewType = Type.GetType(viewName);
            if (viewType == null)
                throw new InvalidOperationException($"Не найден View '{viewName}' для ViewModel '{viewModelType.FullName}'");

            return viewType;
        }
    }
}
