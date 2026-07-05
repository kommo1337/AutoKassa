using Microsoft.Extensions.DependencyInjection;

namespace AutoKassa.ViewModels.Reports
{
    /// <summary>
    /// Реализация фабрики отчётных ViewModel на основе DI-контейнера.
    /// </summary>
    public class ReportViewModelFactory : IReportViewModelFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ReportViewModelFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public BalanceReportViewModel CreateBalanceReportViewModel()
            => _serviceProvider.GetRequiredService<BalanceReportViewModel>();

        /// <inheritdoc />
        public CategoryReportViewModel CreateCategoryReportViewModel()
            => _serviceProvider.GetRequiredService<CategoryReportViewModel>();

        /// <inheritdoc />
        public TransactionDetailReportViewModel CreateTransactionDetailReportViewModel()
            => _serviceProvider.GetRequiredService<TransactionDetailReportViewModel>();

        /// <inheritdoc />
        public DebtReportViewModel CreateDebtReportViewModel()
            => _serviceProvider.GetRequiredService<DebtReportViewModel>();
    }
}
