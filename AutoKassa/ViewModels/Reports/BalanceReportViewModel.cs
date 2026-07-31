using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoKassa.Models.Reports;
using AutoKassa.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace AutoKassa.ViewModels.Reports
{
    /// <summary>
    /// ViewModel для отчета "Баланс за период"
    /// </summary>
    public class BalanceReportViewModel : BaseReportViewModel
    {
        private readonly IReportService _reportService;
        private readonly IExportService _exportService;

        private BalanceReport _report;
        private PlotModel _plotModel;

        public BalanceReportViewModel(
            IReportService reportService,
            IExportService exportService,
            IDialogService dialogService,
            IToastNotificationService toastService) : base(dialogService, toastService)
        {
            _reportService = reportService;
            _exportService = exportService;

            // Инициализация пустой модели графика
            _plotModel = new PlotModel();

            // Инициализация отложена до первого отображения через InitializeAsync
        }

        #region Свойства

        public override string ReportName => "Баланс за период";

        /// <summary>
        /// Данные отчета
        /// </summary>
        public BalanceReport Report
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

        #endregion

        #region Методы

        /// <summary>
        /// Загрузка данных отчета
        /// </summary>
        protected override bool CheckHasData() => Report?.DailyBalances?.Any() == true;

        protected override async Task LoadDataAsync(CancellationToken ct = default)
        {
            Report = await _reportService.GenerateBalanceReportAsync(DateFrom, DateTo, SelectedPaymentType, ct);

            UpdateChart();
        }

        /// <summary>
        /// Обновление графика
        /// </summary>
        private void UpdateChart()
        {
            if (Report == null || Report.DailyBalances == null || !Report.DailyBalances.Any())
                return;

            PlotModel = BuildChartModel(Report);
        }

        private static PlotModel BuildChartModel(BalanceReport report)
        {
            // Создаём новую модель графика
            var model = new PlotModel
            {
                Title = "Баланс за период"
            };

            // Добавляем легенду
            model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.BottomCenter,
                LegendOrientation = LegendOrientation.Horizontal
            });

            // Ось X (даты)
            var dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "dd.MM",
                Angle = 45,
                FontSize = 11,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230)
            };
            model.Axes.Add(dateAxis);

            // Ось Y (значения)
            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                StringFormat = "N0",
                FontSize = 11,
                MinimumPadding = 0.1,
                MaximumPadding = 0.1,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230)
            };
            model.Axes.Add(valueAxis);

            // Серия доходов (область)
            var incomeSeries = new AreaSeries
            {
                Title = "Доходы",
                Color = OxyColor.Parse("#4CAF50"),
                Fill = OxyColor.FromAColor(100, OxyColor.Parse("#4CAF50")),
                StrokeThickness = 2
            };
            foreach (var daily in report.DailyBalances)
            {
                incomeSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(daily.Date), (double)daily.Income));
            }
            model.Series.Add(incomeSeries);

            // Серия расходов (область)
            var expenseSeries = new AreaSeries
            {
                Title = "Расходы",
                Color = OxyColor.Parse("#F44336"),
                Fill = OxyColor.FromAColor(100, OxyColor.Parse("#F44336")),
                StrokeThickness = 2
            };
            foreach (var daily in report.DailyBalances)
            {
                expenseSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(daily.Date), (double)daily.Expense));
            }
            model.Series.Add(expenseSeries);

            // Серия баланса (линия)
            var balanceSeries = new LineSeries
            {
                Title = "Баланс",
                Color = OxyColor.Parse("#2196F3"),
                StrokeThickness = 3,
                MarkerType = MarkerType.Circle,
                MarkerSize = 6,
                MarkerFill = OxyColor.Parse("#2196F3"),
                MarkerStroke = OxyColors.White,
                MarkerStrokeThickness = 2
            };
            foreach (var daily in report.DailyBalances)
            {
                balanceSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(daily.Date), (double)daily.Balance));
            }
            model.Series.Add(balanceSeries);

            return model;
        }

        /// <summary>
        /// Экспорт в PDF
        /// </summary>
        protected override async Task ExportToPdfAsync()
        {
            if (Report == null)
            {
                _toastService.ShowInfo("Сначала сформируйте отчет");
                return;
            }

            var filePath = await _exportService.ExportBalanceReportToPdfAsync(Report);
            ShowExportedToast(filePath, "PDF");
        }

        /// <summary>
        /// Экспорт в Excel
        /// </summary>
        protected override async Task ExportToExcelAsync()
        {
            if (Report == null)
            {
                _toastService.ShowInfo("Сначала сформируйте отчет");
                return;
            }

            var filePath = await _exportService.ExportBalanceReportToExcelAsync(Report);
            ShowExportedToast(filePath, "Excel");
        }

        #endregion
    }
}
