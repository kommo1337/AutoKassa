using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;
using AutoKassa.Services;
using OxyPlot;
using OxyPlot.Series;

namespace AutoKassa.ViewModels.Reports
{
    public class CategoryReportViewModel : BaseReportViewModel
    {
        private readonly IReportService _reportService;
        private readonly IExportService _exportService;
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly ISettingsService _settingsService;

        private DateTime _dateFrom;
        private DateTime _dateTo;
        private OperationType _selectedOperationType;
        private CategoryReport _report;
        private PlotModel _plotModel;
        private ObservableCollection<DonutSliceItem> _donutSlices = new();
        private PaymentType? _selectedPaymentType;
        private ObservableCollection<ExpandableCategoryItem> _expandableItems = new();

        // Modal edit
        private bool _isModalOpen;
        private TransactionEditViewModel _editViewModel;

        public CategoryReportViewModel(
            IReportService reportService,
            IExportService exportService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            ITransactionService transactionService,
            ICategoryService categoryService,
            ISettingsService settingsService) : base(dialogService, toastService)
        {
            _reportService = reportService;
            _exportService = exportService;
            _transactionService = transactionService;
            _categoryService = categoryService;
            _settingsService = settingsService;

            _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo = DateTime.Now.Date;
            _selectedOperationType = OperationType.Expense;
            _plotModel = new PlotModel();

            ShowExpensesCommand = new RelayCommand(_ => SetOperationType(OperationType.Expense));
            ShowIncomeCommand = new RelayCommand(_ => SetOperationType(OperationType.Income));

            SetPaymentAllCommand     = new RelayCommand(_ => SelectedPaymentType = null);
            SetPaymentCashCommand    = new RelayCommand(_ => SelectedPaymentType = PaymentType.Cash);
            SetPaymentNonCashCommand = new RelayCommand(_ => SelectedPaymentType = PaymentType.NonCash);

            ToggleCategoryCommand = new RelayCommand(param =>
            {
                if (param is ExpandableCategoryItem item)
                    item.IsExpanded = !item.IsExpanded;
            });

            EditTransactionCommand = new RelayCommand(param =>
            {
                if (param is Transaction t)
                    EditTransaction(t);
            });

            DeleteTransactionCommand = new RelayCommand(async param =>
            {
                if (param is Transaction t)
                    await DeleteTransactionAsync(t);
            });

            MarkInitialized();
        }

        #region Properties

        public override string ReportName => "Структура по категориям";

        public DateTime DateFrom
        {
            get => _dateFrom;
            set
            {
                if (SetProperty(ref _dateFrom, value))
                {
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
                    ValidateDateRange();
                    AutoRefresh();
                }
            }
        }

        public OperationType SelectedOperationType
        {
            get => _selectedOperationType;
            set
            {
                if (SetProperty(ref _selectedOperationType, value))
                {
                    OnPropertyChanged(nameof(IsExpenseSelected));
                    OnPropertyChanged(nameof(IsIncomeSelected));
                }
            }
        }

        public bool IsExpenseSelected => SelectedOperationType == OperationType.Expense;
        public bool IsIncomeSelected => SelectedOperationType == OperationType.Income;

        public CategoryReport Report
        {
            get => _report;
            set => SetProperty(ref _report, value);
        }

        public PlotModel PlotModel
        {
            get => _plotModel;
            set => SetProperty(ref _plotModel, value);
        }

        public ObservableCollection<DonutSliceItem> DonutSlices
        {
            get => _donutSlices;
            set => SetProperty(ref _donutSlices, value);
        }

        public ObservableCollection<ExpandableCategoryItem> ExpandableItems
        {
            get => _expandableItems;
            set => SetProperty(ref _expandableItems, value);
        }

        public bool IsModalOpen
        {
            get => _isModalOpen;
            set => SetProperty(ref _isModalOpen, value);
        }

        public TransactionEditViewModel EditViewModel
        {
            get => _editViewModel;
            set => SetProperty(ref _editViewModel, value);
        }

        public List<OperationTypeItem> OperationTypes { get; } = new List<OperationTypeItem>
        {
            new OperationTypeItem { Type = OperationType.Expense, Name = "Расходы" },
            new OperationTypeItem { Type = OperationType.Income, Name = "Доходы" }
        };

        #endregion

        #region Commands

        public ICommand ShowExpensesCommand { get; }
        public ICommand ShowIncomeCommand { get; }

        public PaymentType? SelectedPaymentType
        {
            get => _selectedPaymentType;
            set
            {
                if (SetProperty(ref _selectedPaymentType, value))
                {
                    OnPropertyChanged(nameof(IsPaymentAll));
                    OnPropertyChanged(nameof(IsPaymentCash));
                    OnPropertyChanged(nameof(IsPaymentNonCash));
                    AutoRefresh();
                }
            }
        }

        public bool IsPaymentAll     => !SelectedPaymentType.HasValue;
        public bool IsPaymentCash    => SelectedPaymentType == PaymentType.Cash;
        public bool IsPaymentNonCash => SelectedPaymentType == PaymentType.NonCash;

        public ICommand SetPaymentAllCommand     { get; }
        public ICommand SetPaymentCashCommand    { get; }
        public ICommand SetPaymentNonCashCommand { get; }

        public ICommand ToggleCategoryCommand { get; }
        public ICommand EditTransactionCommand { get; }
        public ICommand DeleteTransactionCommand { get; }

        #endregion

        #region Methods

        private void ValidateDateRange()
        {
            if (DateFrom > DateTo)
                DateTo = DateFrom;
        }

        private void SetOperationType(OperationType type)
        {
            SelectedOperationType = type;
            AutoRefresh();
        }

        protected override async Task LoadDataAsync()
        {
            Report = await _reportService.GenerateCategoryReportAsync(DateFrom, DateTo, SelectedOperationType, SelectedPaymentType);
            UpdateChart();
            BuildExpandableItems();
        }

        private void BuildExpandableItems()
        {
            var items = new ObservableCollection<ExpandableCategoryItem>();
            if (Report?.CategoryItems != null)
            {
                foreach (var ci in Report.CategoryItems)
                {
                    items.Add(new ExpandableCategoryItem(ci));
                }
            }
            ExpandableItems = items;
        }

        private void UpdateChart()
        {
            if (Report?.CategoryItems == null || !Report.CategoryItems.Any())
                return;

            var slices = new ObservableCollection<DonutSliceItem>();
            double currentAngle = 0;

            foreach (var item in Report.CategoryItems)
            {
                double sweep = Report.TotalAmount > 0
                    ? (double)item.Amount / (double)Report.TotalAmount * 360.0
                    : 0;

                if (sweep <= 0) continue;

                slices.Add(DonutSliceItem.Create(
                    item.CategoryName, item.Color,
                    item.Percentage, item.Amount, item.TransactionCount,
                    currentAngle, sweep));

                currentAngle += sweep;
            }

            DonutSlices = slices;
        }

        public void SetPeriod(string period)
        {
            BatchUpdate(() =>
            {
                var now = DateTime.Now;
                switch (period)
                {
                    case "Today":
                        DateFrom = now.Date;
                        DateTo = now.Date;
                        break;
                    case "Week":
                        DateFrom = now.Date.AddDays(-(int)now.DayOfWeek);
                        DateTo = now.Date;
                        break;
                    case "Month":
                        DateFrom = new DateTime(now.Year, now.Month, 1);
                        DateTo = now.Date;
                        break;
                    case "Quarter":
                        var quarter = (now.Month - 1) / 3;
                        DateFrom = new DateTime(now.Year, quarter * 3 + 1, 1);
                        DateTo = now.Date;
                        break;
                    case "Year":
                        DateFrom = new DateTime(now.Year, 1, 1);
                        DateTo = now.Date;
                        break;
                }
            });
        }

        private void EditTransaction(Transaction transaction)
        {
            var vm = new TransactionEditViewModel(_transactionService, _categoryService, _dialogService, _settingsService, _toastService);
            vm.InitializeForEdit(transaction);
            vm.OnSaved = () => { IsModalOpen = false; RunAsync(GenerateReportAsync); };
            vm.OnCancelled = () => { IsModalOpen = false; };
            EditViewModel = vm;
            IsModalOpen = true;
        }

        private async Task DeleteTransactionAsync(Transaction transaction)
        {
            var deletedId = transaction.Id;
            try
            {
                await _transactionService.DeleteAsync(deletedId);
                await GenerateReportAsync();

                _toastService.ShowDeleteWithUndo(
                    "Операция удалена",
                    async () =>
                    {
                        await _transactionService.RestoreAsync(deletedId);
                        await GenerateReportAsync();
                    });
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ошибка удаления: {ex.Message}");
            }
        }

        protected override async Task ExportToPdfAsync()
        {
            if (Report == null)
            {
                _toastService.ShowInfo("Сначала сформируйте отчет");
                return;
            }

            var filePath = await _exportService.ExportCategoryReportToPdfAsync(Report);
            _toastService.ShowWithAction("Отчет PDF сохранён", "Открыть", () =>
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }));
        }

        protected override async Task ExportToExcelAsync()
        {
            if (Report == null)
            {
                _toastService.ShowInfo("Сначала сформируйте отчет");
                return;
            }

            var filePath = await _exportService.ExportCategoryReportToExcelAsync(Report);
            _toastService.ShowWithAction("Отчет Excel сохранён", "Открыть", () =>
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }));
        }

        #endregion
    }

    /// <summary>
    /// Обёртка над CategoryReportItem для поддержки раскрытия/свёртывания
    /// </summary>
    public class ExpandableCategoryItem : ViewModelBase
    {
        private bool _isExpanded;

        public ExpandableCategoryItem(CategoryReportItem item)
        {
            Item = item;
        }

        public CategoryReportItem Item { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public string CategoryName => Item.CategoryName;
        public string Color => Item.Color;
        public decimal Amount => Item.Amount;
        public double Percentage => Item.Percentage;
        public int TransactionCount => Item.TransactionCount;
        public List<Transaction> Transactions => Item.Transactions;
    }

    public class OperationTypeItem
    {
        public OperationType Type { get; set; }
        public string Name { get; set; }
    }
}
