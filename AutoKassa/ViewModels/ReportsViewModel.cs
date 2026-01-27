using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Services;
using AutoKassa.ViewModels.Reports;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для экрана отчетов
    /// </summary>
    public class ReportsViewModel : ViewModelBase
    {
        private ViewModelBase _currentReport;

        public ReportsViewModel()
        {
            // Команды навигации между отчетами
            ShowBalanceReportCommand = new RelayCommand(_ => ShowBalanceReport());
            ShowCategoryReportCommand = new RelayCommand(_ => ShowCategoryReport());
            ShowTransactionDetailReportCommand = new RelayCommand(_ => ShowTransactionDetailReport());
        }

        #region Свойства

        /// <summary>
        /// Текущий отображаемый отчет
        /// </summary>
        public ViewModelBase CurrentReport
        {
            get => _currentReport;
            set => SetProperty(ref _currentReport, value);
        }

        #endregion

        #region Команды

        public ICommand ShowBalanceReportCommand { get; }
        public ICommand ShowCategoryReportCommand { get; }
        public ICommand ShowTransactionDetailReportCommand { get; }

        #endregion

        #region Методы

        private void ShowBalanceReport()
        {
            var viewModel = App.GetService<BalanceReportViewModel>();
            CurrentReport = viewModel;
        }

        private void ShowCategoryReport()
        {
            var viewModel = App.GetService<CategoryReportViewModel>();
            CurrentReport = viewModel;
        }

        private void ShowTransactionDetailReport()
        {
            var viewModel = App.GetService<TransactionDetailReportViewModel>();
            CurrentReport = viewModel;
        }

        #endregion
    }
}