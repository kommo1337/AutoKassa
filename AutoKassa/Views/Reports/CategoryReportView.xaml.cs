using System.Windows;
using System.Windows.Controls;
using AutoKassa.ViewModels.Reports;

namespace AutoKassa.Views.Reports
{
    public partial class CategoryReportView : UserControl
    {
        public CategoryReportView()
        {
            InitializeComponent();
        }

        private void QuickFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string period)
            {
                var viewModel = DataContext as CategoryReportViewModel;
                viewModel?.SetPeriod(period);

                // Автоматически формируем отчет
                if (viewModel?.GenerateCommand.CanExecute(null) == true)
                {
                    viewModel.GenerateCommand.Execute(null);
                }
            }
        }
    }
}
