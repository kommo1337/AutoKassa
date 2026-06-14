using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models.Enums;
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

        private DateTime _dateFrom;
        private DateTime _dateTo;
        private BalanceReport _report;
        private PlotModel _plotModel;
        private PaymentType? _selectedPaymentType;

        public BalanceReportViewModel(
            IReportService reportService,
            IExportService exportService,
            IDialogService dialogService,
            IToastNotificationService toastService) : base(dialogService, toastService)
        {
            _reportService = reportService;
            _exportService = exportService;

            // По умолчанию - текущий месяц
            _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo = DateTime.Now.Date;
            ActivePeriodPreset = "Month";

            // Инициализация пустой модели графика
            _plotModel = new PlotModel();

            // Команды фильтра типа оплаты
            SetPaymentAllCommand        = new RelayCommand(_ => SelectedPaymentType = null);
            SetPaymentCashCommand       = new RelayCommand(_ => SelectedPaymentType = PaymentType.Cash);
            SetPaymentNonCashCommand    = new RelayCommand(_ => SelectedPaymentType = PaymentType.NonCash);
            SetPaymentCreditCardCommand = new RelayCommand(_ => SelectedPaymentType = PaymentType.CreditCard);

            // Инициализация отложена до первого отображения через InitializeAsync
        }

        #region Свойства

        public override string ReportName => "Баланс за период";

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
                    OnDateChangedByUser();
                    ValidateDateRange();
                    AutoRefresh();
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
                    OnDateChangedByUser();
                    ValidateDateRange();
                    AutoRefresh();
                }
            }
        }

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

        // Фильтр типа оплаты
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
                    OnPropertyChanged(nameof(IsPaymentCreditCard));
                    AutoRefresh();
                }
            }
        }

        public bool IsPaymentAll        => !SelectedPaymentType.HasValue;
        public bool IsPaymentCash       => SelectedPaymentType == PaymentType.Cash;
        public bool IsPaymentNonCash    => SelectedPaymentType == PaymentType.NonCash;
        public bool IsPaymentCreditCard => SelectedPaymentType == PaymentType.CreditCard;

        public ICommand SetPaymentAllCommand        { get; }
        public ICommand SetPaymentCashCommand       { get; }
        public ICommand SetPaymentNonCashCommand    { get; }
        public ICommand SetPaymentCreditCardCommand { get; }

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
        /// Быстрые фильтры периодов
        /// </summary>
        public void SetPeriod(string period)
        {
            BatchUpdate(() =>
            {
                var (from, to) = AutoKassa.Helpers.PeriodHelper.GetDateRange(period);
                DateFrom = from;
                DateTo = to;
            });
            ActivePeriodPreset = period;
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
            _toastService.ShowWithAction("Отчет PDF сохранён", "Открыть", () =>
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }));
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
            _toastService.ShowWithAction("Отчет Excel сохранён", "Открыть", () =>
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }));
        }

        #endregion
    }
}