using AutoKassa.Helpers;
using AutoKassa.Models.Enums;
using System;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Реализация координатора периода дашборда.
    /// </summary>
    public class DashboardPeriodCoordinator : IDashboardPeriodCoordinator
    {
        /// <inheritdoc />
        public DashboardPeriod SetPeriod(PeriodType period)
        {
            return period switch
            {
                PeriodType.Today => Create(PeriodHelper.GetDateRange("Today"), "ЗА СЕГОДНЯ", "Сегодня"),
                PeriodType.Week => Create(PeriodHelper.GetDateRange("Week"), "ЗА НЕДЕЛЮ", "Неделя"),
                PeriodType.Month => Create(PeriodHelper.GetDateRange("Month"), "ЗА МЕСЯЦ", "Месяц"),
                PeriodType.Year => Create(PeriodHelper.GetDateRange("Year"), "ЗА ГОД", "Год"),
                PeriodType.AllTime => Create((new DateTime(2000, 1, 1), DateTime.Today), "ЗА ВСЁ ВРЕМЯ", "Всё время"),
                PeriodType.Custom => Create((DateTime.Today, DateTime.Today), "ЗА ПЕРИОД", "Период"),
                _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Неизвестный тип периода")
            };
        }

        /// <inheritdoc />
        public DashboardPeriod SetCustomPeriod(DateTime from, DateTime to)
        {
            return new DashboardPeriod(from.Date, to.Date, "ЗА ПЕРИОД", "Период");
        }

        private static DashboardPeriod Create((DateTime From, DateTime To) range, string label, string buttonLabel)
        {
            return new DashboardPeriod(range.From, range.To, label, buttonLabel);
        }
    }
}
