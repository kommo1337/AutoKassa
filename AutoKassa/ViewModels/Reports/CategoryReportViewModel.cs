using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;
using AutoKassa.Services;
using OxyPlot;
using OxyPlot.Series;

namespace AutoKassa.ViewModels.Reports
{
    /// <summary>
    /// ViewModel для отчета "Структура по категориям"
    /// </summary>
    public class CategoryReportViewModel : BaseReportViewModel
    {
        private readonly IReportService _reportService;
        private readonly IExportService _exportService;

        private DateTime _dateFrom;
        private DateTime _dateTo;
        private OperationType _selectedOperationType;
        private CategoryReport _report;
        private PlotModel _plotModel;
        private ObservableCollection<DonutSliceItem> _donutSlices = new();

        public CategoryReportViewModel(
            IReportService reportService,
            IExportService exportService,
            IDialogService dialogService) : base(dialogService)
        {
            _reportService = reportService;
            _exportService = exportService;

            // По умолчанию - текущий месяц, расходы
            _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo = DateTime.Now.Date;
            _selectedOperationType = OperationType.Expense;

            // Инициализация пустой модели графика
            _plotModel = new PlotModel();

            // Команды для переключения типа операций
            ShowExpensesCommand = new RelayCommand(_ => SetOperationType(OperationType.Expense));
            ShowIncomeCommand = new RelayCommand(_ => SetOperationType(OperationType.Income));
        }

        #region Свойства

        public override string ReportName => "Структура по категориям";

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
        /// Выбранный тип операций
        /// </summary>
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

        /// <summary>
        /// Выбраны расходы
        /// </summary>
        public bool IsExpenseSelected => SelectedOperationType == OperationType.Expense;

        /// <summary>
        /// Выбраны доходы
        /// </summary>
        public bool IsIncomeSelected => SelectedOperationType == OperationType.Income;

        /// <summary>
        /// Данные отчета
        /// </summary>
        public CategoryReport Report
        {
            get => _report;
            set => SetProperty(ref _report, value);
        }

        /// <summary>
        /// Модель графика OxyPlot
        /// </summary>
        public PlotModel PlotModel
        {
            get => _plotModel;
            set => SetProperty(ref _plotModel, value);
        }

        /// <summary>
        /// Срезы пончика для кастомной диаграммы
        /// </summary>
        public ObservableCollection<DonutSliceItem> DonutSlices
        {
            get => _donutSlices;
            set => SetProperty(ref _donutSlices, value);
        }

        /// <summary>
        /// Список типов операций для ComboBox
        /// </summary>
        public List<OperationTypeItem> OperationTypes { get; } = new List<OperationTypeItem>
        {
            new OperationTypeItem { Type = OperationType.Expense, Name = "Расходы" },
            new OperationTypeItem { Type = OperationType.Income, Name = "Доходы" }
        };

        #endregion

        #region Команды

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
        /// Установить тип операций
        /// </summary>
        private void SetOperationType(OperationType type)
        {
            SelectedOperationType = type;
        }

        /// <summary>
        /// Загрузка данных отчета
        /// </summary>
        protected override async Task LoadDataAsync()
        {
            Report = await _reportService.GenerateCategoryReportAsync(DateFrom, DateTo, SelectedOperationType);

            // Обновляем график
            UpdateChart();
        }

        /// <summary>
        /// Обновление диаграммы
        /// </summary>
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
                var filePath = await _exportService.ExportCategoryReportToPdfAsync(Report);
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
                var filePath = await _exportService.ExportCategoryReportToExcelAsync(Report);
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

    /// <summary>
    /// Элемент для списка типов операций
    /// </summary>
    public class OperationTypeItem
    {
        public OperationType Type { get; set; }
        public string Name { get; set; }
    }
}
