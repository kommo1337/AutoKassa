using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Services;
using AutoKassa.ViewModels.Reports;

namespace AutoKassa.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        private ViewModelBase _currentReport;

        public ReportsViewModel()
        {
            ShowBalanceReportCommand           = new RelayCommand(_ => ShowBalanceReport());
            ShowCategoryReportCommand          = new RelayCommand(_ => ShowCategoryReport());
            ShowTransactionDetailReportCommand = new RelayCommand(_ => ShowTransactionDetailReport());

            // По умолчанию открываем первую вкладку
            ShowBalanceReport();
        }

        public ViewModelBase CurrentReport
        {
            get => _currentReport;
            set
            {
                SetProperty(ref _currentReport, value);
                OnPropertyChanged(nameof(IsBalanceActive));
                OnPropertyChanged(nameof(IsCategoryActive));
                OnPropertyChanged(nameof(IsDetailActive));
            }
        }

        public bool IsBalanceActive  => CurrentReport is BalanceReportViewModel;
        public bool IsCategoryActive => CurrentReport is CategoryReportViewModel;
        public bool IsDetailActive   => CurrentReport is TransactionDetailReportViewModel;

        public ICommand ShowBalanceReportCommand           { get; }
        public ICommand ShowCategoryReportCommand          { get; }
        public ICommand ShowTransactionDetailReportCommand { get; }

        private void ShowBalanceReport()
        {
            CurrentReport = App.GetService<BalanceReportViewModel>();
        }

        private void ShowCategoryReport()
        {
            CurrentReport = App.GetService<CategoryReportViewModel>();
        }

        private void ShowTransactionDetailReport()
        {
            CurrentReport = App.GetService<TransactionDetailReportViewModel>();
        }
    }
}
