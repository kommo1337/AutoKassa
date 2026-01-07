using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Services;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel главного окна приложения
    /// </summary>
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly ILockService _lockService;
        private string _title = "АвтоКасса";

        public MainWindowViewModel(
            INavigationService navigationService,
            ILockService lockService)
        {
            _navigationService = navigationService;
            _lockService = lockService;

            // Подписка на изменение текущего View
            _navigationService.CurrentViewChanged += OnCurrentViewChanged;

            // Команды навигации
            NavigateToDashboardCommand = new RelayCommand(_ => NavigateToDashboard());
            NavigateToTransactionsCommand = new RelayCommand(_ => NavigateToTransactions());
            NavigateToReportsCommand = new RelayCommand(_ => NavigateToReports());
            NavigateToCategoriesCommand = new RelayCommand(_ => NavigateToCategories());
            NavigateToSettingsCommand = new RelayCommand(_ => NavigateToSettings());

            // Команда блокировки
            LockCommand = new RelayCommand(_ => Lock());

            // Запускаем таймер автоблокировки
            _lockService.StartAutoLockTimer();
        }

        #region Свойства

        /// <summary>
        /// Заголовок окна
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Текущий отображаемый ViewModel
        /// </summary>
        public ViewModelBase CurrentView => _navigationService.CurrentView;

        #endregion

        #region Команды

        public ICommand NavigateToDashboardCommand { get; }
        public ICommand NavigateToTransactionsCommand { get; }
        public ICommand NavigateToReportsCommand { get; }
        public ICommand NavigateToCategoriesCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }
        public ICommand LockCommand { get; }

        #endregion

        #region Методы навигации

        private void NavigateToDashboard()
        {
            // TODO: Реализовать после создания DashboardViewModel
            // _navigationService.NavigateTo<DashboardViewModel>();
        }

        private void NavigateToTransactions()
        {
            _navigationService.NavigateTo<TransactionsViewModel>();
        }

        private void NavigateToReports()
        {
            // TODO: Реализовать после создания ReportsViewModel
            // _navigationService.NavigateTo<ReportsViewModel>();
        }

        private void NavigateToCategories()
        {
            // TODO: Реализовать после создания CategoriesViewModel
            // _navigationService.NavigateTo<CategoriesViewModel>();
        }

        private void NavigateToSettings()
        {
            // TODO: Реализовать после создания SettingsViewModel
            // _navigationService.NavigateTo<SettingsViewModel>();
        }

        #endregion

        #region Блокировка

        private void Lock()
        {
            _lockService.Lock();
        }

        #endregion

        #region Обработчики событий

        private void OnCurrentViewChanged()
        {
            OnPropertyChanged(nameof(CurrentView));
        }

        #endregion
    }
}