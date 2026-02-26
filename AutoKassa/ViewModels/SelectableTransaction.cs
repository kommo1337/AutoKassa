using AutoKassa.Helpers;
using AutoKassa.Models;
using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// Обёртка над Transaction с поддержкой выделения для массового удаления
    /// </summary>
    public class SelectableTransaction : ViewModelBase
    {
        private bool _isSelected;

        public Transaction Transaction { get; }
        public Action? SelectionChanged { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                    SelectionChanged?.Invoke();
            }
        }

        public SelectableTransaction(Transaction transaction)
        {
            Transaction = transaction;
        }
    }

    /// <summary>
    /// Группа операций за один день с поддержкой выделения
    /// </summary>
    public class SelectableDateGroup
    {
        public DateTime Date { get; set; }
        public decimal DayTotal { get; set; }
        public ObservableCollection<SelectableTransaction> Items { get; set; } = new();

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
