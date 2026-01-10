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
    }
}
