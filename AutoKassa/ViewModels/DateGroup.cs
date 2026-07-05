using AutoKassa.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// Группа операций за один день на дашборде.
    /// </summary>
    public class DateGroup
    {
        public DateTime Date { get; set; }
        public decimal DayTotal { get; set; }
        public ObservableCollection<Transaction> Items { get; set; } = new();

        public string DateLabel => Date == DateTime.Today
            ? "Сегодня"
            : Date == DateTime.Today.AddDays(-1)
                ? "Вчера"
                : Date.ToString("d MMMM", new CultureInfo("ru-RU"));

        public string DayTotalFormatted => DayTotal >= 0
            ? $"+{DayTotal:N0} ₽"
            : $"{DayTotal:N0} ₽";

        public string DayTotalColor => DayTotal >= 0 ? "#22c55e" : "#ef4444";
    }
}
