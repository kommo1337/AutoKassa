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

        #endregion

        #region Методы

        private void ShowBalanceReport()
        {
            var viewModel = App.GetService<BalanceReportViewModel>();
            CurrentReport = viewModel;
        }

        #endregion
    }
}