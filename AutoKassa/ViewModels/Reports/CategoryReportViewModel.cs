using System;
using System.Collections.Generic;
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
        /// Обновление графика
        /// </summary>
        private void UpdateChart()
        {
            if (Report == null || Report.CategoryItems == null || !Report.CategoryItems.Any())
                return;

            // Создаём новую модель графика
            var model = new PlotModel
            {
                Title = SelectedOperationType == OperationType.Expense
                    ? "Структура расходов"
                    : "Структура доходов"
            };

            // Создаем круговую диаграмму
            var pieSeries = new PieSeries
            {
                StrokeThickness = 2,
                InsideLabelPosition = 0.5,
                AngleSpan = 360,
                StartAngle = 0,
                InsideLabelFormat = "{1:0.0}%",
                OutsideLabelFormat = "{0}",
                TickHorizontalLength = 0,
                TickRadialLength = 0
            };

            // Добавляем данные по категориям
            foreach (var item in Report.CategoryItems)
            {
                pieSeries.Slices.Add(new PieSlice(item.CategoryName, (double)item.Amount)
                {
                    Fill = OxyColor.Parse(item.Color),
                    IsExploded = false
                });
            }

            model.Series.Add(pieSeries);

            PlotModel = model;
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
