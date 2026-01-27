using System.Windows;
using System.Windows.Controls;
using AutoKassa.ViewModels.Reports;

namespace AutoKassa.Views.Reports
{
    /// <summary>
    /// Логика взаимодействия для TransactionDetailReportView.xaml
    /// </summary>
    public partial class TransactionDetailReportView : UserControl
    {
        public TransactionDetailReportView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Обработчик быстрых фильтров периода
        /// </summary>
        private void QuickFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string period)
            {
                var viewModel = DataContext as TransactionDetailReportViewModel;
                viewModel?.SetPeriod(period);
            }
        }
    }
}
