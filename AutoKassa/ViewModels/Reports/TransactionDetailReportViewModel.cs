using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <summary>
    /// ViewModel для отчета "Детализация операций"
    /// </summary>
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

        public TransactionDetailReportViewModel(
            IReportService reportService,
            ICategoryService categoryService,
            IExportService exportService,
            IDialogService dialogService) : base(dialogService)
        {
            _reportService = reportService;
            _categoryService = categoryService;
            _exportService = exportService;

            // По умолчанию - текущий месяц
            _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo = DateTime.Now.Date;

            _categories = new List<Category>();

            // Команды для переключения фильтра типа операций
            ShowAllCommand = new RelayCommand(_ => SetOperationType(null));
            ShowExpensesCommand = new RelayCommand(_ => SetOperationType(OperationType.Expense));
            ShowIncomeCommand = new RelayCommand(_ => SetOperationType(OperationType.Income));

            // Загружаем категории
            _ = LoadCategoriesAsync();
        }

        #region Свойства

        public override string ReportName => "Детализация операций";

        /// <summary>
        /// Дата начала периода
        /// </summary>
        public DateTime DateFrom
        {
            get => _dateFrom;
            set
            {
                if (SetProperty(ref _dateFrom, value))
                {
                    ValidateDateRange();
                }
            }
        }

        /// <summary>
        /// Дата окончания периода
        /// </summary>
        public DateTime DateTo
        {
            get => _dateTo;
            set
            {
                if (SetProperty(ref _dateTo, value))
                {
                    ValidateDateRange();
                }
            }
        }

        /// <summary>
        /// Выбранный тип операций (null = все)
        /// </summary>
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

        /// <summary>
        /// Выбраны все операции
        /// </summary>
        public bool IsAllSelected => !SelectedOperationType.HasValue;

        /// <summary>
        /// Выбраны расходы
        /// </summary>
        public bool IsExpenseSelected => SelectedOperationType == OperationType.Expense;

        /// <summary>
        /// Выбраны доходы
        /// </summary>
        public bool IsIncomeSelected => SelectedOperationType == OperationType.Income;

        /// <summary>
        /// Выбранная категория для фильтра (null = все)
        /// </summary>
        public Category SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        /// <summary>
        /// Список категорий для фильтра
        /// </summary>
        public List<Category> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        /// <summary>
        /// Данные отчета
        /// </summary>
        public TransactionDetailReport Report
        {
            get => _report;
            set => SetProperty(ref _report, value);
        }

        #endregion

        #region Команды

        public ICommand ShowAllCommand { get; }
        public ICommand ShowExpensesCommand { get; }
        public ICommand ShowIncomeCommand { get; }

        #endregion

        #region Методы

        /// <summary>
        /// Валидация диапазона дат
        /// </summary>
        private void ValidateDateRange()
        {
            if (DateFrom > DateTo)
            {
                DateTo = DateFrom;
            }
        }

        /// <summary>
        /// Загрузка списка категорий
        /// </summary>
        private async Task LoadCategoriesAsync()
        {
            try
            {
                var allCategories = await _categoryService.GetAllAsync();
                Categories = new List<Category> { new Category { Id = 0, Name = "Все категории" } }
                    .Concat(allCategories.OrderBy(c => c.Name))
                    .ToList();
                SelectedCategory = Categories.First();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
        }

        /// <summary>
        /// Установить тип операций
        /// </summary>
        private void SetOperationType(OperationType? type)
        {
            SelectedOperationType = type;
        }

        /// <summary>
        /// Загрузка данных отчета
        /// </summary>
        protected override async Task LoadDataAsync()
        {
            var categoryId = SelectedCategory?.Id > 0 ? SelectedCategory.Id : (int?)null;
            Report = await _reportService.GenerateTransactionDetailReportAsync(
                DateFrom,
                DateTo,
                SelectedOperationType,
                categoryId);
        }

        /// <summary>
        /// Быстрые фильтры периодов
        /// </summary>
        public void SetPeriod(string period)
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
        }

        /// <summary>
        /// Экспорт в PDF
        /// </summary>
        protected override async void ExportToPdf()
        {
            if (Report == null)
            {
                _dialogService.ShowWarning("Сначала сформируйте отчет");
                return;
            }

            try
            {
                var filePath = await _exportService.ExportTransactionDetailReportToPdfAsync(Report);
                _dialogService.ShowInfo($"Отчет сохранен:\n{filePath}");

                // Открываем файл
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка экспорта: {ex.Message}");
            }
        }

        /// <summary>
        /// Экспорт в Excel
        /// </summary>
        protected override async void ExportToExcel()
        {
            if (Report == null)
            {
                _dialogService.ShowWarning("Сначала сформируйте отчет");
                return;
            }

            try
            {
                var filePath = await _exportService.ExportTransactionDetailReportToExcelAsync(Report);
                _dialogService.ShowInfo($"Отчет сохранен:\n{filePath}");

                // Открываем файл
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка экспорта: {ex.Message}");
            }
        }

        #endregion
    }
}
