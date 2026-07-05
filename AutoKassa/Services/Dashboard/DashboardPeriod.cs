using System;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Представляет выбранный период дашборда: границы дат и подписи.
    /// </summary>
    public record DashboardPeriod(DateTime From, DateTime To, string Label, string ButtonLabel);
}
