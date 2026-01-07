using System;
using System.Threading.Tasks;
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
    }
}