using AutoKassa.Helpers;
using AutoKassa.Services;

namespace AutoKassa.Tests.Infrastructure
{
    /// <summary>
    /// Фейк INavigationService для тестов: навигацию не выполняет,
    /// только хранит CurrentView и позволяет вызвать событие смены view.
    /// </summary>
    public class FakeNavigationService : INavigationService
    {
        public ViewModelBase? CurrentView { get; set; }

        public event Action? CurrentViewChanged;

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase { }

        public void NavigateTo<TViewModel>(object parameter) where TViewModel : ViewModelBase { }

        /// <summary>Вручную вызвать событие смены текущего view.</summary>
        public void RaiseCurrentViewChanged() => CurrentViewChanged?.Invoke();
    }
}
