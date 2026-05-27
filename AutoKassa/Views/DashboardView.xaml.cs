using AutoKassa.Models;
using AutoKassa.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoKassa.Views
{
    /// <summary>
    /// Code-behind для DashboardView
    /// </summary>
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void ShowAllTransactions_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DashboardViewModel viewModel)
            {
                viewModel.NavigateToAllTransactionsCommand.Execute(null);
            }
        }

        /// <summary>
        /// Обработчик клика по строке операции: одиночный — выделение, двойной — редактирование
        /// </summary>
        private void TransactionItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is Transaction transaction &&
                DataContext is DashboardViewModel vm)
            {
                vm.SelectedTransaction = transaction;

                if (e.ClickCount == 2 && vm.EditTransactionCommand.CanExecute(transaction))
                    vm.EditTransactionCommand.Execute(transaction);
            }
        }
    }
}
