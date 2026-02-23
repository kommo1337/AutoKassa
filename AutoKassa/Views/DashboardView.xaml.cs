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
    }
}
