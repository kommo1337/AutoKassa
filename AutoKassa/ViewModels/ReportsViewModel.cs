using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Services;
using AutoKassa.ViewModels.Reports;

namespace AutoKassa.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        private BaseReportViewModel _currentReport;

        public ReportsViewModel()
        {
            ShowBalanceReportCommand           = new RelayCommand(_ => ShowBalanceReport());
            ShowCategoryReportCommand          = new RelayCommand(_ => ShowCategoryReport());
            ShowTransactionDetailReportCommand = new RelayCommand(_ => ShowTransactionDetailReport());

            // По умолчанию открываем отчёт «Баланс» сразу при переходе на вкладку
            ShowBalanceReport();
        }

        public BaseReportViewModel CurrentReport
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
            (_currentReport as IDisposable)?.Dispose();
            var vm = App.GetService<BalanceReportViewModel>();
            CurrentReport = vm;
            RunAsync(vm.InitializeAsync);
        }

        private void ShowCategoryReport()
        {
            (_currentReport as IDisposable)?.Dispose();
            var vm = App.GetService<CategoryReportViewModel>();
            CurrentReport = vm;
            RunAsync(vm.InitializeAsync);
        }

        private void ShowTransactionDetailReport()
        {
            (_currentReport as IDisposable)?.Dispose();
            var vm = App.GetService<TransactionDetailReportViewModel>();
            CurrentReport = vm;
            RunAsync(vm.InitializeAsync);
        }
    }
}
