using AutoKassa.Models;
using AutoKassa.ViewModels;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Сервис для работы с массовым выделением операций.
    /// </summary>
    public interface ITransactionSelectionService
    {
        /// <summary>
        /// Возвращает список выделенных операций из групп.
        /// </summary>
        /// <param name="groups">Группы операций.</param>
        /// <returns>Список выделенных операций.</returns>
        IReadOnlyList<Transaction> GetSelected(IEnumerable<SelectableDateGroup> groups);

        /// <summary>
        /// Снимает выделение со всех операций в группах.
        /// </summary>
        /// <param name="groups">Группы операций.</param>
        /// <param name="refreshState">Делегат для обновления состояния выделения после сброса.</param>
        void ClearSelection(IEnumerable<SelectableDateGroup> groups, Action? refreshState = null);

        /// <summary>
        /// Возвращает текстовую метку с количеством выделенных записей.
        /// </summary>
        /// <param name="count">Количество выделенных записей.</param>
        /// <returns>Текстовая метка.</returns>
        string GetSelectionLabel(int count);

        /// <summary>
        /// Возвращает склонение слова "запись" для указанного количества.
        /// </summary>
        /// <param name="count">Количество записей.</param>
        /// <returns>Склонённое слово.</returns>
        string GetCountWord(int count);
    }
}
