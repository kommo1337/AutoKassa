using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.ViewModels;
using System.Collections.ObjectModel;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Реализация сервиса группировки операций по датам и расчёта итогов.
    /// </summary>
    public class TransactionGroupingService : ITransactionGroupingService
    {
        private readonly IToastNotificationService _toastService;

        /// <summary>
        /// Создаёт экземпляр <see cref="TransactionGroupingService"/>.
        /// </summary>
        /// <param name="toastService">Сервис toast-уведомлений для inline-форм групп.</param>
        public TransactionGroupingService(IToastNotificationService toastService)
        {
            _toastService = toastService;
        }

        /// <inheritdoc />
        public async Task<TransactionGroupingResult> BuildGroupsAsync(
            IEnumerable<Transaction> transactions,
            IEnumerable<Category> categories,
            IEnumerable<CreditCard> creditCards,
            Func<SelectableDateGroup, Task> onGroupSave,
            Action selectionChanged,
            CancellationToken ct = default)
        {
            var transactionsSnapshot = transactions.ToList();
            var categoriesCollection = new ObservableCollection<Category>(categories);
            var creditCardsCollection = new ObservableCollection<CreditCard>(creditCards);

            var (groups, totalIncome, totalExpense) = await Task.Run(() =>
            {
                var income = transactionsSnapshot
                    .Where(t => t.Type == OperationType.Income)
                    .Sum(t => t.Amount);

                var expense = transactionsSnapshot
                    .Where(t => t.Type == OperationType.Expense)
                    .Sum(t => t.Amount);

                var groups = new List<SelectableDateGroup>();
                var grouped = transactionsSnapshot
                    .GroupBy(t => t.Date.Date)
                    .OrderByDescending(g => g.Key);

                foreach (var g in grouped)
                {
                    var dayTotal = g.Sum(t => t.Type == OperationType.Income ? t.Amount : -t.Amount);
                    var items = new ObservableCollection<SelectableTransaction>(
                        g.OrderByDescending(t => t.CreatedAt)
                         .Select(t => new SelectableTransaction(t)));

                    var group = new SelectableDateGroup
                    {
                        Date = g.Key,
                        DayTotal = dayTotal,
                        Items = items
                    };

                    group.InitInline(categoriesCollection, creditCardsCollection, onGroupSave, _toastService);

                    foreach (var item in group.Items)
                        item.SelectionChanged = selectionChanged;

                    groups.Add(group);
                }

                return (groups, income, expense);
            }, ct);

            return new TransactionGroupingResult(groups, totalIncome, totalExpense);
        }
    }
}
