using System;
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
        Task<BalanceReport> GenerateBalanceReportAsync(DateTime dateFrom, DateTime dateTo);

        /// <summary>
        /// Получить начальный баланс на дату
        /// </summary>
        Task<decimal> GetInitialBalanceAsync(DateTime date);

        /// <summary>
        /// Сформировать отчет "Структура по категориям"
        /// </summary>
        Task<CategoryReport> GenerateCategoryReportAsync(DateTime dateFrom, DateTime dateTo, OperationType operationType);
    }
}