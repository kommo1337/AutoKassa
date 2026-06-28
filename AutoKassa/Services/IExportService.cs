using System.Threading.Tasks;
using AutoKassa.Models.Reports;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис экспорта отчетов
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// Экспорт отчета "Баланс за период" в PDF
        /// </summary>
        Task<string> ExportBalanceReportToPdfAsync(BalanceReport report);

        /// <summary>
        /// Экспорт отчета "Баланс за период" в Excel
        /// </summary>
        Task<string> ExportBalanceReportToExcelAsync(BalanceReport report);

        /// <summary>
        /// Экспорт отчета "Структура по категориям" в PDF
        /// </summary>
        Task<string> ExportCategoryReportToPdfAsync(CategoryReport report);

        /// <summary>
        /// Экспорт отчета "Структура по категориям" в Excel
        /// </summary>
        Task<string> ExportCategoryReportToExcelAsync(CategoryReport report);

        /// <summary>
        /// Экспорт отчета "Детализация операций" в PDF
        /// </summary>
        Task<string> ExportTransactionDetailReportToPdfAsync(TransactionDetailReport report);

        /// <summary>
        /// Экспорт отчета "Детализация операций" в Excel
        /// </summary>
        Task<string> ExportTransactionDetailReportToExcelAsync(TransactionDetailReport report);

        /// <summary>
        /// Экспорт отчета по долгам в PDF
        /// </summary>
        Task<string> ExportDebtReportToPdfAsync(DebtReport report);

        /// <summary>
        /// Экспорт отчета по долгам в Excel
        /// </summary>
        Task<string> ExportDebtReportToExcelAsync(DebtReport report);
    }
}
