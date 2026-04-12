using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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

        private DateTime _dateFrom;
        private DateTime _dateTo;
        private OperationType? _selectedOperationType;
        private Category _selectedCategory;
        private TransactionDetailReport _report;
        private List<Category> _categories;
        private List<TransactionDetailGroup> _groupedTransactions = new();

        public TransactionDetailReportViewModel(
            IReportService reportService,
            ICategoryService categoryService,
            IExportService exportService,
            IDialogService dialogService) : base(dialogService)
        {
            _reportService   = reportService;
            _categoryService = categoryService;
            _exportService   = exportService;

            _dateFrom   = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo     = DateTime.Now.Date;
            _categories = new List<Category>();

            ShowAllCommand      = new RelayCommand(_ => SetOperationType(null));
            ShowExpensesCommand = new RelayCommand(_ => SetOperationType(OperationType.Expense));
            ShowIncomeCommand   = new RelayCommand(_ => SetOperationType(OperationType.Income));

            RunAsync(LoadCategoriesAsync);
        }

        public override string ReportName => "Детализация операций";

        public DateTime DateFrom
        {
            get => _dateFrom;
            set { if (SetProperty(ref _dateFrom, value)) ValidateDateRange(); }
        }

        public DateTime DateTo
        {
            get => _dateTo;
            set { if (SetProperty(ref _dateTo, value)) ValidateDateRange(); }
        }

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
            set => SetProperty(ref _selectedCategory, value);
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

        private void ValidateDateRange()
        {
            if (DateFrom > DateTo) DateTo = DateFrom;
        }

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
                _dialogService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
        }

        private void SetOperationType(OperationType? type)
        {
            SelectedOperationType = type;
        }

        protected override async Task LoadDataAsync()
        {
            var categoryId = SelectedCategory?.Id > 0 ? SelectedCategory.Id : (int?)null;
            Report = await _reportService.GenerateTransactionDetailReportAsync(
                DateFrom, DateTo, SelectedOperationType, categoryId);
            BuildGroups();
        }

        private void BuildGroups()
        {
            if (Report?.Transactions == null || Report.Transactions.Count == 0)
            {
                GroupedTransactions = new List<TransactionDetailGroup>();
                return;
            }

            var today = DateTime.Today;
            GroupedTransactions = Report.Transactions
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

        public void SetPeriod(string period)
        {
            var now = DateTime.Now;
            switch (period)
            {
                case "Today":
                    DateFrom = now.Date;
                    DateTo   = now.Date;
                    break;
                case "Week":
                    DateFrom = now.Date.AddDays(-(int)now.DayOfWeek);
                    DateTo   = now.Date;
                    break;
                case "Month":
                    DateFrom = new DateTime(now.Year, now.Month, 1);
                    DateTo   = now.Date;
                    break;
                case "Quarter":
                    var q    = (now.Month - 1) / 3;
                    DateFrom = new DateTime(now.Year, q * 3 + 1, 1);
                    DateTo   = now.Date;
                    break;
                case "Year":
                    DateFrom = new DateTime(now.Year, 1, 1);
                    DateTo   = now.Date;
                    break;
            }
        }

        protected override async void ExportToPdf()
        {
            if (Report == null) { _dialogService.ShowWarning("Сначала сформируйте отчет"); return; }
            try
            {
                var path = await _exportService.ExportTransactionDetailReportToPdfAsync(Report);
                _dialogService.ShowInfo($"Отчет сохранен:\n{path}");
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { _dialogService.ShowError($"Ошибка экспорта: {ex.Message}"); }
        }

        protected override async void ExportToExcel()
        {
            if (Report == null) { _dialogService.ShowWarning("Сначала сформируйте отчет"); return; }
            try
            {
                var path = await _exportService.ExportTransactionDetailReportToExcelAsync(Report);
                _dialogService.ShowInfo($"Отчет сохранен:\n{path}");
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { _dialogService.ShowError($"Ошибка экспорта: {ex.Message}"); }
        }
    }
}
