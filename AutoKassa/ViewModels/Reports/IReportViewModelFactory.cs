namespace AutoKassa.ViewModels.Reports
{
    /// <summary>
    /// Фабрика для создания ViewModel отчётов.
    /// Изолирует создание отчётных VM от статического Service Locator.
    /// </summary>
    public interface IReportViewModelFactory
    {
        /// <summary>
        /// Создать ViewModel отчёта "Баланс за период".
        /// </summary>
        BalanceReportViewModel CreateBalanceReportViewModel();

        /// <summary>
        /// Создать ViewModel отчёта "Структура по категориям".
        /// </summary>
        CategoryReportViewModel CreateCategoryReportViewModel();

        /// <summary>
        /// Создать ViewModel детального отчёта по операциям.
        /// </summary>
        TransactionDetailReportViewModel CreateTransactionDetailReportViewModel();

        /// <summary>
        /// Создать ViewModel отчёта по долгам.
        /// </summary>
        DebtReportViewModel CreateDebtReportViewModel();
    }
}
