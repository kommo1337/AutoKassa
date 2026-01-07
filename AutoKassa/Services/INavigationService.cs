using AutoKassa.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoKassa.Services
{
    public interface INavigationService
    {
        /// <summary>
        /// Текущий отображаемый ViewModel
        /// </summary>
        ViewModelBase CurrentView { get; }

        /// <summary>
        /// Событие изменения текущего View
        /// </summary>
        event Action CurrentViewChanged;

        /// <summary>
        /// Навигация к указанному ViewModel
        /// </summary>
        void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

        /// <summary>
        /// Навигация к указанному ViewModel с параметром
        /// </summary>
        void NavigateTo<TViewModel>(object parameter) where TViewModel : ViewModelBase;
    }
}
