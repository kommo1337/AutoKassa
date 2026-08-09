namespace AutoKassa.ViewModels
{
    /// <summary>
    /// Маркер для ViewModel страниц верхнего уровня (вкладок главного окна).
    /// Такие ViewModel регистрируются в DI как Singleton и живут всё время работы
    /// приложения: NavigationService не вызывает у них Dispose при переключении
    /// вкладок (их освобождает DI-контейнер при завершении), а логика
    /// _isInitialized/_needsRefresh в OnNavigatedTo решает, нужно ли перезагружать данные.
    /// </summary>
    public interface IPageViewModel
    {
    }
}
