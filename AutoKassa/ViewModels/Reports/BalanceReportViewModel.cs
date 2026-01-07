using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoKassa.Models.Reports;
using AutoKassa.Services;
//using LiveChartsCore;
//using LiveChartsCore.SkiaSharpView;
//using LiveChartsCore.SkiaSharpView.Painting;
//using SkiaSharp;

namespace AutoKassa.ViewModels.Reports
{
    /// <summary>
    /// ViewModel для отчета "Баланс за период"
    /// </summary>
    public class BalanceReportViewModel : BaseReportViewModel
    {

        private readonly IReportService _reportService;

        private DateTime _dateFrom;
        private DateTime _dateTo;
        private BalanceReport _report;
        //private ISeries[] _series;

        public BalanceReportViewModel(
    IReportService reportService,
    IDialogService dialogService) : base(dialogService)
        {
            _reportService = reportService;

            // По умолчанию - текущий месяц
            _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo = DateTime.Now.Date;

            // ДОБАВЬТЕ ЭТУ СТРОКУ
            //Series = Array.Empty<ISeries>();
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
        /// Данные отчета
        /// </summary>
        public BalanceReport Report
        {
            get => _report;
            set => SetProperty(ref _report, value);
        }

        /// <summary>
        /// Серии данных для графика
        /// </summary>
        //public ISeries[] Series
        //{
        //    get => _series;
        //    set => SetProperty(ref _series, value);
        //}

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
        protected override async Task LoadDataAsync()
        {
            Report = await _reportService.GenerateBalanceReportAsync(DateFrom, DateTo);

            // Обновляем график
            //UpdateChart();
        }

        /// <summary>
        /// Обновление графика
        /// </summary>
        //private void UpdateChart()
        //{
        //    if (Report == null || Report.DailyBalances == null || !Report.DailyBalances.Any())
        //        return;

        //    // Подготовка данных
        //    var dates = Report.DailyBalances.Select(d => d.Date.ToString("dd.MM")).ToArray();
        //    var balances = Report.DailyBalances.Select(d => (double)d.Balance).ToArray();
        //    var incomes = Report.DailyBalances.Select(d => (double)d.Income).ToArray();
        //    var expenses = Report.DailyBalances.Select(d => -(double)d.Expense).ToArray(); // Отрицательные для отображения вниз

            //// Создаём серии данных
            //Series = new ISeries[]
            //{
            //    // Столбцы доходов
            //    new ColumnSeries<double>
            //    {
            //        Name = "Доходы",
            //        Values = incomes,
            //        Fill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
            //        Stroke = null,
            //        MaxBarWidth = 20
            //    },

            //    // Столбцы расходов
            //    new ColumnSeries<double>
            //    {
            //        Name = "Расходы",
            //        Values = expenses,
            //        Fill = new SolidColorPaint(SKColor.Parse("#F44336")),
            //        Stroke = null,
            //        MaxBarWidth = 20
            //    },

            //    // Линия баланса
            //    new LineSeries<double>
            //    {
            //        Name = "Баланс",
            //        Values = balances,
            //        Stroke = new SolidColorPaint(SKColor.Parse("#2196F3")) { StrokeThickness = 3 },
            //        Fill = null,
            //        GeometrySize = 8,
            //        GeometryFill = new SolidColorPaint(SKColor.Parse("#2196F3")),
            //        GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 }
            //    }
        //    };
        //}

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

        #endregion
    }
}