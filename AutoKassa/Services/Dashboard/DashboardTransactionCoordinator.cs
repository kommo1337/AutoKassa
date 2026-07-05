using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services.Transactions;
using AutoKassa.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Реализация координатора операций дашборда.
    /// </summary>
    public class DashboardTransactionCoordinator : IDashboardTransactionCoordinator
    {
        private readonly ITransactionService _transactionService;
        private readonly ITransactionEditViewModelFactory _transactionEditViewModelFactory;
        private readonly IToastNotificationService _toastService;

        /// <summary>
        /// Создаёт экземпляр координатора.
        /// </summary>
        public DashboardTransactionCoordinator(
            ITransactionService transactionService,
            ITransactionEditViewModelFactory transactionEditViewModelFactory,
            IToastNotificationService toastService)
        {
            _transactionService = transactionService;
            _transactionEditViewModelFactory = transactionEditViewModelFactory;
            _toastService = toastService;
        }

        /// <inheritdoc />
        public TransactionEditViewModel OpenAdd(DashboardTransactionCallbacks callbacks)
        {
            var vm = _transactionEditViewModelFactory.CreateForAdd();
            AttachCallbacks(vm, callbacks);
            return vm;
        }

        /// <inheritdoc />
        public TransactionEditViewModel OpenEdit(Transaction transaction, DashboardTransactionCallbacks callbacks)
        {
            var vm = _transactionEditViewModelFactory.CreateForEdit(transaction);
            AttachCallbacks(vm, callbacks);
            return vm;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Transaction transaction, Func<Task> onUndoReloadAsync)
        {
            if (transaction == null)
                return;

            var deletedId = transaction.Id;

            try
            {
                await _transactionService.DeleteAsync(deletedId);

                _toastService.ShowDeleteWithUndo(
                    "Операция удалена",
                    async () =>
                    {
                        await _transactionService.RestoreAsync(deletedId);
                        await onUndoReloadAsync();
                    });
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ошибка удаления: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public DashboardOptimisticResult ApplyOptimisticAdd(DashboardOptimisticState state, Transaction transaction)
        {
            if (transaction.Date < state.DateFrom || transaction.Date > state.DateTo)
            {
                return new DashboardOptimisticResult(
                    state.RecentTransactions,
                    state.GroupedTransactions,
                    state.TotalIncome,
                    state.TotalExpense,
                    state.RecentTransactions.Count > 0);
            }

            // Обновляем сводку
            var totalIncome = state.TotalIncome;
            var totalExpense = state.TotalExpense;

            if (transaction.Type == OperationType.Income)
                totalIncome += transaction.Amount;
            else
                totalExpense += transaction.Amount;

            // Обновляем список последних операций
            var recent = state.RecentTransactions.ToList();
            recent.Insert(0, transaction);
            if (recent.Count > 100)
                recent = recent.Take(100).ToList();

            // Обновляем группировку по дням
            var groups = state.GroupedTransactions.ToList();
            var dateKey = transaction.Date.Date;
            var existingGroup = groups.FirstOrDefault(g => g.Date == dateKey);

            if (existingGroup != null)
            {
                var items = existingGroup.Items.ToList();
                items.Insert(0, transaction);
                existingGroup.Items = new ObservableCollection<Transaction>(items);
                existingGroup.DayTotal += transaction.Type == OperationType.Income ? transaction.Amount : -transaction.Amount;
            }
            else
            {
                var newGroup = new DateGroup
                {
                    Date = dateKey,
                    DayTotal = transaction.Type == OperationType.Income ? transaction.Amount : -transaction.Amount,
                    Items = new ObservableCollection<Transaction>(new[] { transaction })
                };
                groups.Insert(0, newGroup);
                groups = groups.OrderByDescending(g => g.Date).ToList();
            }

            return new DashboardOptimisticResult(
                recent,
                groups,
                totalIncome,
                totalExpense,
                true);
        }

        private static void AttachCallbacks(TransactionEditViewModel vm, DashboardTransactionCallbacks callbacks)
        {
            vm.OnSaved = callbacks.OnSaved;
            vm.OnSavedKeepOpen = callbacks.OnSavedKeepOpen;
            vm.OnCancelled = callbacks.OnCancelled;
        }
    }
}
