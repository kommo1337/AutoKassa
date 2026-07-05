using AutoKassa.Models.Enums;
using System;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Координатор расчёта периода дашборда.
    /// </summary>
    public interface IDashboardPeriodCoordinator
    {
        /// <summary>
        /// Установить один из предустановленных периодов.
        /// </summary>
        DashboardPeriod SetPeriod(PeriodType period);

        /// <summary>
        /// Установить произвольный период по датам.
        /// </summary>
        DashboardPeriod SetCustomPeriod(DateTime from, DateTime to);
    }
}
