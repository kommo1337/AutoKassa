using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;
using AutoKassa.Services;

namespace AutoKassa.ViewModels.Reports
{
    public class TransactionDetailReportViewModel : BaseReportViewModel
    {
        private readonly IReportService _reportService;
        private readonly ICategoryService _categoryService;
        private readonly IExportService _exportService;

        private OperationType? _selectedOperationType;
        private Category _selectedCategory;
        private TransactionDetailReport _report;
        private List<Category> _categories;
        private List<TransactionDetailGroup> _groupedTransactions = new();

        public TransactionDetailReportViewModel(
            IReportService reportService,
            ICategoryService categoryService,
            IExportService exportService,
            IDialogService dialogService,
            IToastNotificationService toastService) : base(dialogService, toastService)
        {
            _reportService   = reportService;
            _categoryService = categoryService;
            _exportService   = exportService;

            _categories = new List<Category>();

            ShowAllCommand      = new RelayCommand(_ => SetOperationType(null));
            ShowExpensesCommand = new RelayCommand(_ => SetOperationType(OperationType.Expense));
            ShowIncomeCommand   = new RelayCommand(_ => SetOperationType(OperationType.Income));

            // Инициализация отложена до первого отображения через InitializeAsync
        }

        public override string ReportName => "Детализация операций";

        public OperationType? SelectedOperationType
        {
            get => _selectedOperationType;
            set
            {
                if (SetProperty(ref _selectedOperationType, value))
                {
                    OnPropertyChanged(nameof(IsAllSelected));
                    OnPropertyChanged(nameof(IsExpenseSelected));
                    OnPropertyChanged(nameof(IsIncomeSelected));
                }
            }
        }

        public bool IsAllSelected     => !SelectedOperationType.HasValue;
        public bool IsExpenseSelected => SelectedOperationType == OperationType.Expense;
        public bool IsIncomeSelected  => SelectedOperationType == OperationType.Income;

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetProperty(ref _selectedCategory, value)) AutoRefresh(); }
        }

        public List<Category> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public TransactionDetailReport Report
        {
            get => _report;
            set => SetProperty(ref _report, value);
        }

        public List<TransactionDetailGroup> GroupedTransactions
        {
            get => _groupedTransactions;
            set => SetProperty(ref _groupedTransactions, value);
        }

        public ICommand ShowAllCommand      { get; }
        public ICommand ShowExpensesCommand { get; }
        public ICommand ShowIncomeCommand   { get; }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var all = await _categoryService.GetAllAsync();
                Categories = new List<Category> { new Category { Id = 0, Name = "Все категории" } }
                    .Concat(all.OrderBy(c => c.Name))
                    .ToList();
                SelectedCategory = Categories.First();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
        }

        private void SetOperationType(OperationType? type)
        {
            SelectedOperationType = type;
            AutoRefresh();
        }

        public override async Task InitializeAsync()
        {
            await LoadCategoriesAsync();
            await base.InitializeAsync();
        }

        protected override bool CheckHasData() => Report?.Transactions?.Any() == true;

        protected override async Task LoadDataAsync(CancellationToken ct = default)
        {
            var categoryId = SelectedCategory?.Id > 0 ? SelectedCategory.Id : (int?)null;
            Report = await _reportService.GenerateTransactionDetailReportAsync(
                DateFrom, DateTo, SelectedOperationType, categoryId, SelectedPaymentType, ct);

            GroupedTransactions = BuildGroups(Report);
        }

        private static List<TransactionDetailGroup> BuildGroups(TransactionDetailReport report)
        {
            if (report?.Transactions == null || report.Transactions.Count == 0)
            {
                return new List<TransactionDetailGroup>();
            }

            var today = DateTime.Today;
            return report.Transactions
                .GroupBy(t => t.Date.Date)
                .OrderByDescending(g => g.Key)
                .Select(g =>
                {
                    var income  = g.Where(t => t.Type == OperationType.Income).Sum(t => t.Amount);
                    var expense = g.Where(t => t.Type == OperationType.Expense).Sum(t => t.Amount);
                    var net     = income - expense;

                    return new TransactionDetailGroup
                    {
                        Date              = g.Key,
                        DateLabel         = FormatDateLabel(g.Key, today),
                        DayTotalFormatted = net >= 0 ? $"+{net:N0} ₽" : $"{net:N0} ₽",
                        DayTotalColor     = net >= 0 ? "#22c55e" : "#ef4444",
                        Items             = g.OrderByDescending(t => t.Date).ToList()
                    };
                })
                .ToList();
        }

        private static string FormatDateLabel(DateTime date, DateTime today)
        {
            if (date == today)             return "Сегодня";
            if (date == today.AddDays(-1)) return "Вчера";
            return date.ToString("d MMMM, dddd", new CultureInfo("ru-RU"));
        }

        protected override async Task ExportToPdfAsync()
        {
            if (Report == null) { _toastService.ShowInfo("Сначала сформируйте отчет"); return; }
            var path = await _exportService.ExportTransactionDetailReportToPdfAsync(Report);
            ShowExportedToast(path, "PDF");
        }

        protected override async Task ExportToExcelAsync()
        {
            if (Report == null) { _toastService.ShowInfo("Сначала сформируйте отчет"); return; }
            var path = await _exportService.ExportTransactionDetailReportToExcelAsync(Report);
            ShowExportedToast(path, "Excel");
        }
    }
}
