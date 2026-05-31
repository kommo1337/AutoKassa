namespace AutoKassa.Services
{
    /// <summary>
    /// Интерфейс для ViewModel, которые должны реагировать на навигацию.
    /// Вызывается NavigationService при переключении вкладок.
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// Вызывается при переходе на данный ViewModel (вкладка становится активной)
        /// </summary>
        void OnNavigatedTo();

        /// <summary>
        /// Вызывается при уходе с данного ViewModel (вкладка становится неактивной)
        /// </summary>
        void OnNavigatedFrom();
    }
}
