using AutoKassa.Models;
using AutoKassa.ViewModels;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Фабрика для создания ViewModel редактирования операции.
    /// Убирает ручное создание <see cref="TransactionEditViewModel"/> из ViewModel и открывает DI.
    /// </summary>
    public interface ITransactionEditViewModelFactory
    {
        /// <summary>
        /// Создать ViewModel для добавления новой операции.
        /// </summary>
        TransactionEditViewModel CreateForAdd();

        /// <summary>
        /// Создать ViewModel для редактирования существующей операции.
        /// </summary>
        TransactionEditViewModel CreateForEdit(Transaction transaction);
    }
}
