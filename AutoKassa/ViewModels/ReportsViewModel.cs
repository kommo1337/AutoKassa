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
        private readonly Dictionary<Type, BaseReportViewModel> _reportCache = new();
        private BaseReportViewModel _currentReport;
        private bool _isInitialized;
        private CancellationTokenSource _navigateCts;

        public ReportsViewModel(IDataChangeService dataChangeService)
        {
            _dataChangeService = dataChangeService;

            ShowBalanceReportCommand           = new RelayCommand(_ => ShowBalanceReport());
            ShowCategoryReportCommand          = new RelayCommand(_ => ShowCategoryReport());
            ShowTransactionDetailReportCommand = new RelayCommand(_ => ShowTransactionDetailReport());

            _dataChangeService.DataChanged += OnDataChanged;
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
            foreach (var report in _reportCache.Values)
                report.Invalidate();
        }

        private void ShowBalanceReport()
        {
            if (!_reportCache.TryGetValue(typeof(BalanceReportViewModel), out var vm))
            {
                vm = App.GetService<BalanceReportViewModel>();
                _reportCache[typeof(BalanceReportViewModel)] = vm;
            }
            CurrentReport = vm;
            if (!vm.IsInitialized)
                RunAsync(vm.InitializeAsync);
        }

        private void ShowCategoryReport()
        {
            if (!_reportCache.TryGetValue(typeof(CategoryReportViewModel), out var vm))
            {
                vm = App.GetService<CategoryReportViewModel>();
                _reportCache[typeof(CategoryReportViewModel)] = vm;
            }
            CurrentReport = vm;
            if (!vm.IsInitialized)
                RunAsync(vm.InitializeAsync);
        }

        private void ShowTransactionDetailReport()
        {
            if (!_reportCache.TryGetValue(typeof(TransactionDetailReportViewModel), out var vm))
            {
                vm = App.GetService<TransactionDetailReportViewModel>();
                _reportCache[typeof(TransactionDetailReportViewModel)] = vm;
            }
            CurrentReport = vm;
            if (!vm.IsInitialized)
                RunAsync(vm.InitializeAsync);
        }

        protected override void OnDispose()
        {
            _dataChangeService.DataChanged -= OnDataChanged;
            var navCts = Interlocked.Exchange(ref _navigateCts, null);
            navCts?.Cancel();
            navCts?.Dispose();
            base.OnDispose();
        }
    }
}
