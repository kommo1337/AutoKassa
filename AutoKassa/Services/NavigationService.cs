using AutoKassa.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace AutoKassa.Services
{
    public class NavigationService : INavigationService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private ViewModelBase _currentView;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Текущий отображаемый ViewModel
        /// </summary>
        public ViewModelBase CurrentView
        {
            get => _currentView;
            private set
            {
                _currentView = value;
                CurrentViewChanged?.Invoke();
            }
        }

        /// <summary>
        /// Событие изменения текущего View
        /// </summary>
        public event Action CurrentViewChanged;

        /// <summary>
        /// Навигация к указанному ViewModel
        /// </summary>
        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
            var oldView = _currentView;
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();

            // Не делаем ничего, если уже на этой вкладке
            if (oldView == viewModel)
                return;

            if (oldView is INavigationAware oldNav)
                oldNav.OnNavigatedFrom();

            _currentView = viewModel;

            if (viewModel is INavigationAware newNav)
                newNav.OnNavigatedTo();

            CurrentViewChanged?.Invoke();

            DisposeViewModel(oldView);
        }

        /// <summary>
        /// Навигация к указанному ViewModel с параметром
        /// (параметр можно передать через свойство ViewModel)
        /// </summary>
        public void NavigateTo<TViewModel>(object parameter) where TViewModel : ViewModelBase
        {
            var oldView = _currentView;
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();

            // Не делаем ничего, если уже на этой вкладке
            if (oldView == viewModel)
                return;

            // Если ViewModel имеет метод Initialize, вызываем его с параметром
            var initializeMethod = viewModel.GetType().GetMethod("Initialize");
            initializeMethod?.Invoke(viewModel, new[] { parameter });

            if (oldView is INavigationAware oldNav)
                oldNav.OnNavigatedFrom();

            _currentView = viewModel;

            if (viewModel is INavigationAware newNav)
                newNav.OnNavigatedTo();

            CurrentViewChanged?.Invoke();

            DisposeViewModel(oldView);
        }

        /// <summary>
        /// Освобождает ресурсы уходящего ViewModel
        /// </summary>
        private static void DisposeViewModel(ViewModelBase viewModel)
        {
            if (viewModel is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Ошибка при освобождении ViewModel {ViewModelType}", viewModel.GetType().Name);
                }
            }
        }

        /// <summary>
        /// Освобождает текущий ViewModel при завершении работы сервиса навигации.
        /// </summary>
        public void Dispose()
        {
            DisposeViewModel(_currentView);
            CurrentViewChanged = null;
            GC.SuppressFinalize(this);
        }
    }
}
