using System.Windows;
using System.Windows.Controls;
using AutoKassa.ViewModels.Reports;

namespace AutoKassa.Views.Reports
{
    public partial class BalanceReportView : UserControl
    {
        public BalanceReportView()
        {
            InitializeComponent();
            Unloaded += (_, _) => Plot.Model = null;
        }

        private void QuickFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string period)
            {
                (DataContext as BalanceReportViewModel)?.SetPeriod(period);
            }
        }
    }
}