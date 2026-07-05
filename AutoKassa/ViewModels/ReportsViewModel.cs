using System.Threading;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Services;
using AutoKassa.ViewModels.Reports;

namespace AutoKassa.ViewModels
{
    public class ReportsViewModel : ViewModelBase, INavigationAware
    {
        private readonly IDataChangeService _dataChangeService;
        private readonly IReportViewModelFactory _reportFactory;
        private BaseReportViewModel _currentReport;
        private bool _isInitialized;
        private CancellationTokenSource _navigateCts;

        public ReportsViewModel(IDataChangeService dataChangeService, IReportViewModelFactory reportFactory)
        {
            _dataChangeService = dataChangeService;
            _reportFactory = reportFactory;

            ShowBalanceReportCommand           = new RelayCommand(_ => ShowBalanceReport());
            ShowCategoryReportCommand          = new RelayCommand(_ => ShowCategoryReport());
            ShowTransactionDetailReportCommand = new RelayCommand(_ => ShowTransactionDetailReport());
            ShowDebtReportCommand              = new RelayCommand(_ => ShowDebtReport());

            _dataChangeService.DataChanged += OnDataChanged;
        }

        public BaseReportViewModel CurrentReport
        {
            get => _currentReport;
            set
            {
                var oldReport = _currentReport;
                if (SetProperty(ref _currentReport, value))
                {
                    DisposeReport(oldReport);
                    OnPropertyChanged(nameof(IsBalanceActive));
                    OnPropertyChanged(nameof(IsCategoryActive));
                    OnPropertyChanged(nameof(IsDetailActive));
                    OnPropertyChanged(nameof(IsDebtActive));
                }
            }
        }

        public bool IsBalanceActive  => CurrentReport is BalanceReportViewModel;
        public bool IsCategoryActive => CurrentReport is CategoryReportViewModel;
        public bool IsDetailActive   => CurrentReport is TransactionDetailReportViewModel;
        public bool IsDebtActive     => CurrentReport is DebtReportViewModel;

        public ICommand ShowBalanceReportCommand           { get; }
        public ICommand ShowCategoryReportCommand          { get; }
        public ICommand ShowTransactionDetailReportCommand { get; }
        public ICommand ShowDebtReportCommand              { get; }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;

                var cts = new CancellationTokenSource();
                var old = Interlocked.Exchange(ref _navigateCts, cts);
                old?.Cancel();
                old?.Dispose();

                RunAsync(async () =>
                {
                    try { await Task.Delay(50, cts.Token); }
                    catch (OperationCanceledException) { return; }
                    ShowBalanceReport();
                });
            }
            else if (CurrentReport != null && !CurrentReport.IsInitialized)
            {
                var cts = new CancellationTokenSource();
                var old = Interlocked.Exchange(ref _navigateCts, cts);
                old?.Cancel();
                old?.Dispose();

                RunAsync(async () =>
                {
                    try { await Task.Delay(50, cts.Token); }
                    catch (OperationCanceledException) { return; }
                    await CurrentReport.InitializeAsync();
                });
            }
        }

        public void OnNavigatedFrom() { }

        private void OnDataChanged()
        {
            CurrentReport?.Invalidate();
        }

        private void ShowBalanceReport()
        {
            var vm = _reportFactory.CreateBalanceReportViewModel();
            CurrentReport = vm;
            if (!vm.IsInitialized)
                RunAsync(vm.InitializeAsync);
        }

        private void ShowCategoryReport()
        {
            var vm = _reportFactory.CreateCategoryReportViewModel();
            CurrentReport = vm;
            if (!vm.IsInitialized)
                RunAsync(vm.InitializeAsync);
        }

        private void ShowTransactionDetailReport()
        {
            var vm = _reportFactory.CreateTransactionDetailReportViewModel();
            CurrentReport = vm;
            if (!vm.IsInitialized)
                RunAsync(vm.InitializeAsync);
        }

        private void ShowDebtReport()
        {
            var vm = _reportFactory.CreateDebtReportViewModel();
            CurrentReport = vm;
            if (!vm.IsInitialized)
                RunAsync(vm.InitializeAsync);
        }

        /// <summary>
        /// Освобождает ресурсы старого отчётного ViewModel
        /// </summary>
        private static void DisposeReport(BaseReportViewModel report)
        {
            if (report is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Ошибка при освобождении отчётного ViewModel {ViewModelType}", report.GetType().Name);
                }
            }
        }

        protected override void OnDispose()
        {
            _dataChangeService.DataChanged -= OnDataChanged;
            var navCts = Interlocked.Exchange(ref _navigateCts, null);
            navCts?.Cancel();
            navCts?.Dispose();
            DisposeReport(CurrentReport);
            CurrentReport = null;
            base.OnDispose();
        }
    }
}
