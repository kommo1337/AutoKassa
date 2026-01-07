using AutoKassa.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoKassa.Services
{
    public class NavigationService : INavigationService
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
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
            CurrentView = viewModel;
        }

        /// <summary>
        /// Навигация к указанному ViewModel с параметром
        /// (параметр можно передать через свойство ViewModel)
        /// </summary>
        public void NavigateTo<TViewModel>(object parameter) where TViewModel : ViewModelBase
        {
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();

            // Если ViewModel имеет метод Initialize, вызываем его с параметром
            var initializeMethod = viewModel.GetType().GetMethod("Initialize");
            initializeMethod?.Invoke(viewModel, new[] { parameter });

            CurrentView = viewModel;
        }
    }
}
