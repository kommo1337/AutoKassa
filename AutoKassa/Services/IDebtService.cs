using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с долгами (дебиторская / кредиторская задолженность)
    /// </summary>
    public interface IDebtService
    {
        /// <summary>
        /// Получает список долгов с остатком и статусом.
        /// </summary>
        /// <param name="direction">Направление долга (Income = нам должны, Expense = мы должны)</param>
        /// <param name="counterpartyId">Фильтр по контрагенту</param>
        /// <param name="status">Фильтр по статусу долга</param>
        /// <param name="pageNumber">Номер страницы (начиная с 1)</param>
        /// <param name="pageSize">Размер страницы</param>
        Task<IReadOnlyList<DebtItem>> GetDebtsAsync(
            OperationType? direction = null,
            int? counterpartyId = null,
            DebtStatus? status = null,
            int pageNumber = 1,
            int pageSize = int.MaxValue,
            CancellationToken ct = default);

        /// <summary>
        /// Создаёт операцию-погашение для указанного долга.
        /// </summary>
        Task<Transaction> RepayAsync(int debtTransactionId, decimal amount,
            PaymentType paymentType, DateTime date, string? description = null,
            CancellationToken ct = default);

        /// <summary>
        /// Списывает долг (без создания операции движения денег).
        /// </summary>
        Task WriteOffAsync(int debtTransactionId, CancellationToken ct = default);

        /// <summary>
        /// Получает остаток по долгу.
        /// </summary>
        Task<decimal> GetRemainingAmountAsync(int debtTransactionId, CancellationToken ct = default);

        /// <summary>
        /// Обновляет статусы долгов после изменения/удаления операции погашения.
        /// </summary>
        Task RecalculateStatusAsync(int debtTransactionId, CancellationToken ct = default);
    }
}
