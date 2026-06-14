using System;
using System.Threading;
using System.Threading.Tasks;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для формирования отчетов
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Сформировать отчет "Баланс за период"
        /// </summary>
        Task<BalanceReport> GenerateBalanceReportAsync(DateTime dateFrom, DateTime dateTo, PaymentType? paymentType = null, CancellationToken ct = default);

        /// <summary>
        /// Получить начальный баланс на дату
        /// </summary>
        Task<decimal> GetInitialBalanceAsync(DateTime date, PaymentType? paymentType = null, CancellationToken ct = default);

        /// <summary>
        /// Сформировать отчет "Структура по категориям"
        /// </summary>
        Task<CategoryReport> GenerateCategoryReportAsync(DateTime dateFrom, DateTime dateTo, OperationType operationType, PaymentType? paymentType = null, CancellationToken ct = default);

        /// <summary>
        /// Сформировать отчет "Детализация операций"
        /// </summary>
        Task<TransactionDetailReport> GenerateTransactionDetailReportAsync(DateTime dateFrom, DateTime dateTo, OperationType? operationType = null, int? categoryId = null, PaymentType? paymentType = null, CancellationToken ct = default);

        /// <summary>
        /// Получить данные для окна "Сверка кассы" на указанную дату
        /// </summary>
        Task<ReconciliationData> GetReconciliationDataAsync(DateTime date, CancellationToken ct = default);
    }
}