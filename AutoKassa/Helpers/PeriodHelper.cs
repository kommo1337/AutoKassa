using System;

namespace AutoKassa.Helpers
{
    /// <summary>
    /// Единый расчёт дат для пресетов периодов.
    /// Неделя — с понедельника (ISO 8601), чтобы Dashboard и отчёты показывали одинаковые данные.
    /// </summary>
    public static class PeriodHelper
    {
        public static (DateTime From, DateTime To) GetDateRange(string period)
        {
            var today = DateTime.Today;
            return period switch
            {
                "Today"   => (today, today),
                "Week"    => (today.AddDays(-((int)today.DayOfWeek + 6) % 7), today),
                "Month"   => (new DateTime(today.Year, today.Month, 1), today),
                "Quarter" => (new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1), today),
                "Year"    => (new DateTime(today.Year, 1, 1), today),
                _         => (today, today)
            };
        }
    }
}
