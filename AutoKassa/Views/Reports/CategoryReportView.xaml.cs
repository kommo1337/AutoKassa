using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutoKassa.Models;
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
                (DataContext as CategoryReportViewModel)?.SetPeriod(period);
            }
        }

        private void CategoryHeader_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el &&
                el.DataContext is ExpandableCategoryItem item &&
                DataContext is CategoryReportViewModel vm)
            {
                vm.ToggleCategoryCommand.Execute(item);
            }
        }

        private Transaction _contextTransaction;

        private void TransactionContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu ctx &&
                ctx.PlacementTarget is FrameworkElement el &&
                el.DataContext is Transaction t)
            {
                _contextTransaction = t;
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextTransaction != null && DataContext is CategoryReportViewModel vm)
            {
                vm.EditTransactionCommand.Execute(_contextTransaction);
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextTransaction != null && DataContext is CategoryReportViewModel vm)
            {
                vm.DeleteTransactionCommand.Execute(_contextTransaction);
            }
        }
    }
}
