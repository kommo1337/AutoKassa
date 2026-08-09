using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;
using AutoKassa.Services;
using AutoKassa.Services.Transactions;
using OxyPlot;
using OxyPlot.Series;

namespace AutoKassa.ViewModels.Reports
{
    public class CategoryReportViewModel : BaseReportViewModel
    {
        private readonly IReportService _reportService;
        private readonly IExportService _exportService;
        private readonly ITransactionService _transactionService;
        private readonly IDataChangeService _dataChangeService;
        private readonly ITransactionEditViewModelFactory _transactionEditViewModelFactory;

        private OperationType _selectedOperationType;
        private CategoryReport _report;
        private PlotModel _plotModel;
        private ObservableCollection<DonutSliceItem> _donutSlices = new();
        private ObservableCollection<ExpandableCategoryItem> _expandableItems = new();

        public CategoryReportViewModel(
            IReportService reportService,
            IExportService exportService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            ITransactionService transactionService,
            IDataChangeService dataChangeService,
            ITransactionEditViewModelFactory transactionEditViewModelFactory) : base(dialogService, toastService)
        {
            _reportService = reportService;
            _exportService = exportService;
            _transactionService = transactionService;
            _dataChangeService = dataChangeService;
            _transactionEditViewModelFactory = transactionEditViewModelFactory;

            _selectedOperationType = OperationType.Expense;
            _plotModel = new PlotModel();

            ShowExpensesCommand = new RelayCommand(_ => SetOperationType(OperationType.Expense));
            ShowIncomeCommand = new RelayCommand(_ => SetOperationType(OperationType.Income));

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

            // Инициализация отложена до первого отображения через InitializeAsync
        }

        #region Properties

        public override string ReportName => "Структура по категориям";

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

        public List<OperationTypeItem> OperationTypes { get; } = new List<OperationTypeItem>
        {
            new OperationTypeItem { Type = OperationType.Expense, Name = "Расходы" },
            new OperationTypeItem { Type = OperationType.Income, Name = "Доходы" }
        };

        #endregion

        #region Commands

        public ICommand ShowExpensesCommand { get; }
        public ICommand ShowIncomeCommand { get; }

        public ICommand ToggleCategoryCommand { get; }
        public ICommand EditTransactionCommand { get; }
        public ICommand DeleteTransactionCommand { get; }

        #endregion

        #region Methods

        private void SetOperationType(OperationType type)
        {
            SelectedOperationType = type;
            AutoRefresh();
        }

        protected override bool CheckHasData() => Report?.CategoryItems?.Any() == true;

        protected override async Task LoadDataAsync(CancellationToken ct = default)
        {
            Report = await _reportService.GenerateCategoryReportAsync(DateFrom, DateTo, SelectedOperationType, SelectedPaymentType, ct);
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
                    items.Add(new ExpandableCategoryItem(ci, () => LoadCategoryTransactionsAsync(ci.CategoryId)));
                }
            }
            ExpandableItems = items;
        }

        /// <summary>
        /// Ленивая загрузка операций категории при её раскрытии в отчёте.
        /// </summary>
        private async Task<List<Transaction>> LoadCategoryTransactionsAsync(int categoryId)
        {
            return await _reportService.GetCategoryTransactionsAsync(
                categoryId, DateFrom, DateTo, SelectedOperationType, SelectedPaymentType);
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

        private void EditTransaction(Transaction transaction)
        {
            var vm = _transactionEditViewModelFactory.CreateForEdit(transaction);
            vm.OnSaved = async () => { Modal.Close(); await GenerateReportAsync(); _dataChangeService?.NotifyDataChanged(); };
            vm.OnCancelled = () => { Modal.Close(); };
            Modal.Show(vm);
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
            ShowExportedToast(filePath, "PDF");
        }

        protected override async Task ExportToExcelAsync()
        {
            if (Report == null)
            {
                _toastService.ShowInfo("Сначала сформируйте отчет");
                return;
            }

            var filePath = await _exportService.ExportCategoryReportToExcelAsync(Report);
            ShowExportedToast(filePath, "Excel");
        }

        #endregion
    }

    /// <summary>
    /// Обёртка над CategoryReportItem для поддержки раскрытия/свёртывания.
    /// Операции категории загружаются лениво при первом раскрытии.
    /// </summary>
    public class ExpandableCategoryItem : ViewModelBase
    {
        private readonly Func<Task<List<Transaction>>> _loadTransactions;
        private bool _isExpanded;
        private List<Transaction> _transactions;

        public ExpandableCategoryItem(CategoryReportItem item, Func<Task<List<Transaction>>> loadTransactions)
        {
            Item = item;
            _loadTransactions = loadTransactions;
        }

        public CategoryReportItem Item { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value) && value && _transactions == null)
                    RunAsync(LoadTransactionsAsync);
            }
        }

        public string CategoryName => Item.CategoryName;
        public string Color => Item.Color;
        public decimal Amount => Item.Amount;
        public double Percentage => Item.Percentage;
        public int TransactionCount => Item.TransactionCount;

        /// <summary>
        /// Операции категории — null до первого раскрытия (ленивая загрузка из БД).
        /// </summary>
        public List<Transaction> Transactions
        {
            get => _transactions;
            private set => SetProperty(ref _transactions, value);
        }

        private async Task LoadTransactionsAsync()
        {
            Transactions = await _loadTransactions();
        }
    }

    public class OperationTypeItem
    {
        public OperationType Type { get; set; }
        public string Name { get; set; }
    }
}
