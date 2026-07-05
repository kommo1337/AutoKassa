using AutoKassa.Models;
using AutoKassa.Services.Transactions;
using AutoKassa.ViewModels;
using System;
using System.Threading.Tasks;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Координатор операций дашборда: открытие редактора, удаление, оптимистичное обновление.
    /// </summary>
    public interface IDashboardTransactionCoordinator
    {
        /// <summary>
        /// Открыть редактор для добавления новой операции.
        /// </summary>
        TransactionEditViewModel OpenAdd(DashboardTransactionCallbacks callbacks);

        /// <summary>
        /// Открыть редактор для редактирования существующей операции.
        /// </summary>
        TransactionEditViewModel OpenEdit(Transaction transaction, DashboardTransactionCallbacks callbacks);

        /// <summary>
        /// Удалить операцию с возможностью отмены через toast.
        /// </summary>
        Task DeleteAsync(Transaction transaction, Func<Task> onUndoReloadAsync);

        /// <summary>
        /// Применить оптимистичное добавление операции к состоянию дашборда.
        /// </summary>
        DashboardOptimisticResult ApplyOptimisticAdd(DashboardOptimisticState state, Transaction transaction);
    }
}
