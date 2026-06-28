using System;
using AutoKassa.Models.Enums;

namespace AutoKassa.Models.Reports
{
    /// <summary>
    /// Элемент отчёта по долгам — одна долговая операция с остатком и статусом
    /// </summary>
    public class DebtItem
    {
        /// <summary>
        /// Идентификатор долговой операции
        /// </summary>
        public int TransactionId { get; set; }

        /// <summary>
        /// Дата долговой операции
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Первоначальная сумма долга
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Сумма погашений по долгу
        /// </summary>
        public decimal RepaidAmount { get; set; }

        /// <summary>
        /// Оставшийся долг
        /// </summary>
        public decimal RemainingAmount => Amount - RepaidAmount;

        /// <summary>
        /// Текущий статус долга
        /// </summary>
        public DebtStatus Status { get; set; }

        /// <summary>
        /// Направление долга (Income = нам должны, Expense = мы должны)
        /// </summary>
        public OperationType Direction { get; set; }

        /// <summary>
        /// Идентификатор контрагента
        /// </summary>
        public int? CounterpartyId { get; set; }

        /// <summary>
        /// Название контрагента
        /// </summary>
        public string CounterpartyName { get; set; } = string.Empty;

        /// <summary>
        /// Тип контрагента
        /// </summary>
        public CounterpartyType CounterpartyType { get; set; }

        /// <summary>
        /// Название категории долга
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// Описание операции
        /// </summary>
        public string? Description { get; set; }
    }
}
