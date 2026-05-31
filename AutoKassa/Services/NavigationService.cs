using AutoKassa.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace AutoKassa.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, ViewModelBase> _viewModelCache = new();
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

            if (!_viewModelCache.TryGetValue(typeof(TViewModel), out var viewModel))
            {
                viewModel = _serviceProvider.GetRequiredService<TViewModel>();
                _viewModelCache[typeof(TViewModel)] = viewModel;
            }

            // Не делаем ничего, если уже на этой вкладке
            if (oldView == viewModel)
                return;

            if (oldView is INavigationAware oldNav)
                oldNav.OnNavigatedFrom();

            _currentView = viewModel;

            if (viewModel is INavigationAware newNav)
                newNav.OnNavigatedTo();

            CurrentViewChanged?.Invoke();
        }

        /// <summary>
        /// Навигация к указанному ViewModel с параметром
        /// (параметр можно передать через свойство ViewModel)
        /// </summary>
        public void NavigateTo<TViewModel>(object parameter) where TViewModel : ViewModelBase
        {
            var oldView = _currentView;

            if (!_viewModelCache.TryGetValue(typeof(TViewModel), out var viewModel))
            {
                viewModel = _serviceProvider.GetRequiredService<TViewModel>();
                _viewModelCache[typeof(TViewModel)] = viewModel;
            }

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
        }
    }
}
