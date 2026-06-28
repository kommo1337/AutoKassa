using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;
using AutoKassa.Services;
using AutoKassa.ViewModels;

namespace AutoKassa.ViewModels.Reports
{
    /// <summary>
    /// ViewModel для отчёта по долгам
    /// </summary>
    public class DebtReportViewModel : BaseReportViewModel
    {
        private readonly IReportService _reportService;
        private readonly IExportService _exportService;
        private readonly INavigationService _navigationService;
        private readonly IDebtService _debtService;
        private readonly IDataChangeService _dataChangeService;

        private DateTime _dateFrom;
        private DateTime _dateTo;
        private OperationType? _direction;
        private DebtStatus? _status;
        private DebtReport _report;
        private DebtItem _selectedDebtItem;
        private List<DebtReportGroup> _groupedDebts = new();

        public DebtReportViewModel(
            IReportService reportService,
            IExportService exportService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            INavigationService navigationService,
            IDebtService debtService,
            IDataChangeService dataChangeService) : base(dialogService, toastService)
        {
            _reportService = reportService;
            _exportService = exportService;
            _navigationService = navigationService;
            _debtService = debtService;
            _dataChangeService = dataChangeService;

            _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo = DateTime.Now.Date;
            ActivePeriodPreset = "Month";

            SetDirectionAllCommand = new RelayCommand(_ => Direction = null);
            SetDirectionIncomeCommand = new RelayCommand(_ => Direction = OperationType.Income);
            SetDirectionExpenseCommand = new RelayCommand(_ => Direction = OperationType.Expense);

            SetStatusAllCommand = new RelayCommand(_ => Status = null);
            SetStatusActiveCommand = new RelayCommand(_ => Status = DebtStatus.Active);
            SetStatusRepaidCommand = new RelayCommand(_ => Status = DebtStatus.Repaid);
            SetStatusWrittenOffCommand = new RelayCommand(_ => Status = DebtStatus.WrittenOff);

            SetPeriodCommand = new RelayCommand<string>(period => SetPeriod(period));

            NavigateToCounterpartyCommand = new RelayCommand<DebtItem>(item => NavigateToCounterparty(item));
            RepayDebtCommand = new RelayCommand<DebtItem>(item => OpenRepaymentForm(item), item => item != null && item.Status == DebtStatus.Active);
        }

        #region Свойства

        public override string ReportName => "Отчёт по долгам";

        public DateTime DateFrom
        {
            get => _dateFrom;
            set
            {
                if (SetProperty(ref _dateFrom, value))
                {
                    OnDateChangedByUser();
                    ValidateDateRange();
                    AutoRefresh();
                }
            }
        }

        public DateTime DateTo
        {
            get => _dateTo;
            set
            {
                if (SetProperty(ref _dateTo, value))
                {
                    OnDateChangedByUser();
                    ValidateDateRange();
                    AutoRefresh();
                }
            }
        }

        /// <summary>
        /// Направление долга (Income = нам должны, Expense = мы должны)
        /// </summary>
        public OperationType? Direction
        {
            get => _direction;
            set
            {
                if (SetProperty(ref _direction, value))
                {
                    OnPropertyChanged(nameof(IsDirectionAll));
                    OnPropertyChanged(nameof(IsDirectionIncome));
                    OnPropertyChanged(nameof(IsDirectionExpense));
                    AutoRefresh();
                }
            }
        }

        public bool IsDirectionAll => _direction == null;
        public bool IsDirectionIncome => _direction == OperationType.Income;
        public bool IsDirectionExpense => _direction == OperationType.Expense;

        /// <summary>
        /// Статус долга
        /// </summary>
        public DebtStatus? Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(IsStatusAll));
                    OnPropertyChanged(nameof(IsStatusActive));
                    OnPropertyChanged(nameof(IsStatusRepaid));
                    OnPropertyChanged(nameof(IsStatusWrittenOff));
                    AutoRefresh();
                }
            }
        }

        public bool IsStatusAll => _status == null;
        public bool IsStatusActive => _status == DebtStatus.Active;
        public bool IsStatusRepaid => _status == DebtStatus.Repaid;
        public bool IsStatusWrittenOff => _status == DebtStatus.WrittenOff;

        public DebtReport Report
        {
            get => _report;
            set => SetProperty(ref _report, value);
        }

        public DebtItem SelectedDebtItem
        {
            get => _selectedDebtItem;
            set => SetProperty(ref _selectedDebtItem, value);
        }

        /// <summary>
        /// Долги, сгруппированные по дням для отображения в отчёте.
        /// </summary>
        public List<DebtReportGroup> GroupedDebts
        {
            get => _groupedDebts;
            set => SetProperty(ref _groupedDebts, value);
        }

        public ICommand SetDirectionAllCommand { get; }
        public ICommand SetDirectionIncomeCommand { get; }
        public ICommand SetDirectionExpenseCommand { get; }

        public ICommand SetStatusAllCommand { get; }
        public ICommand SetStatusActiveCommand { get; }
        public ICommand SetStatusRepaidCommand { get; }
        public ICommand SetStatusWrittenOffCommand { get; }

        public ICommand SetPeriodCommand { get; }

        public ICommand NavigateToCounterpartyCommand { get; }
        public ICommand RepayDebtCommand { get; }

        #endregion

        #region Методы

        private void ValidateDateRange()
        {
            if (DateFrom > DateTo)
                DateTo = DateFrom;
        }

        protected override bool CheckHasData() => Report?.Items?.Any() == true;

        protected override async Task LoadDataAsync(CancellationToken ct = default)
        {
            Report = await _reportService.GenerateDebtReportAsync(DateFrom, DateTo, Direction, Status, ct).ConfigureAwait(false);
            GroupedDebts = BuildGroups(Report);
        }

        private static List<DebtReportGroup> BuildGroups(DebtReport report)
        {
            if (report?.Items == null || report.Items.Count == 0)
            {
                return new List<DebtReportGroup>();
            }

            var today = DateTime.Today;
            return report.Items
                .GroupBy(i => i.Date.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new DebtReportGroup
                {
                    Date = g.Key,
                    DateLabel = FormatDateLabel(g.Key, today),
                    DayReceivable = g.Where(i => i.Direction == OperationType.Income && i.Status == DebtStatus.Active).Sum(i => i.RemainingAmount),
                    DayPayable = g.Where(i => i.Direction == OperationType.Expense && i.Status == DebtStatus.Active).Sum(i => i.RemainingAmount),
                    Items = g.OrderByDescending(i => i.Date).ToList()
                })
                .ToList();
        }

        private static string FormatDateLabel(DateTime date, DateTime today)
        {
            if (date == today) return "Сегодня";
            if (date == today.AddDays(-1)) return "Вчера";
            return date.ToString("d MMMM, dddd", new CultureInfo("ru-RU"));
        }

        public void SetPeriod(string period)
        {
            BatchUpdate(() =>
            {
                var (from, to) = PeriodHelper.GetDateRange(period);
                DateFrom = from;
                DateTo = to;
            });
            ActivePeriodPreset = period;
        }

        protected override async Task ExportToPdfAsync()
        {
            if (Report == null)
            {
                _toastService.ShowInfo("Сначала сформируйте отчёт");
                return;
            }

            var filePath = await _exportService.ExportDebtReportToPdfAsync(Report).ConfigureAwait(false);
            _toastService.ShowWithAction("Отчёт PDF сохранён", "Открыть", () =>
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }));
        }

        protected override async Task ExportToExcelAsync()
        {
            if (Report == null)
            {
                _toastService.ShowInfo("Сначала сформируйте отчёт");
                return;
            }

            var filePath = await _exportService.ExportDebtReportToExcelAsync(Report).ConfigureAwait(false);
            _toastService.ShowWithAction("Отчёт Excel сохранён", "Открыть", () =>
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }));
        }

        private void NavigateToCounterparty(DebtItem? item)
        {
            if (item == null) return;

            // Переход к списку контрагентов с выделением нужного контрагента
            if (item.CounterpartyId.HasValue)
                _navigationService.NavigateTo<CounterpartiesViewModel>(item.CounterpartyId.Value);
            else
                _navigationService.NavigateTo<CounterpartiesViewModel>();
        }

        private void OpenRepaymentForm(DebtItem? item)
        {
            if (item == null) return;

            var vm = new DebtRepaymentViewModel(_debtService, _dialogService, _toastService, _dataChangeService);
            vm.Initialize(item);
            vm.OnSaved = async () =>
            {
                Modal.Close();
                await GenerateReportAsync();
                _dataChangeService.NotifyDataChanged();
            };
            vm.OnCancelled = () => Modal.Close();

            Modal.Show(vm);
        }

        #endregion
    }
}
