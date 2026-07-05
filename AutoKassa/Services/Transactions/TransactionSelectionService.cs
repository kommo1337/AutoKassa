using AutoKassa.Models;
using AutoKassa.ViewModels;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Реализация сервиса работы с массовым выделением операций.
    /// </summary>
    public class TransactionSelectionService : ITransactionSelectionService
    {
        /// <inheritdoc />
        public IReadOnlyList<Transaction> GetSelected(IEnumerable<SelectableDateGroup> groups)
        {
            return groups
                .SelectMany(g => g.Items)
                .Where(i => i.IsSelected)
                .Select(i => i.Transaction)
                .ToList();
        }

        /// <inheritdoc />
        public void ClearSelection(IEnumerable<SelectableDateGroup> groups, Action? refreshState = null)
        {
            foreach (var item in groups.SelectMany(g => g.Items).ToList())
            {
                item.SelectionChanged = null;
                item.IsSelected = false;
                item.SelectionChanged = refreshState;
            }

            refreshState?.Invoke();
        }

        /// <inheritdoc />
        public string GetSelectionLabel(int count)
        {
            return $"Выбрано: {count}";
        }

        /// <inheritdoc />
        public string GetCountWord(int count)
        {
            var abs = Math.Abs(count) % 100;
            var mod10 = abs % 10;
            if (abs is >= 11 and <= 19) return "записей";
            return mod10 switch
            {
                1 => "запись",
                2 or 3 or 4 => "записи",
                _ => "записей"
            };
        }
    }
}
