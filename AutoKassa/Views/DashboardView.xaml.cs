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

        /// <summary>
        /// Переход к полному отчету при клике на график
        /// </summary>
        private void Chart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DashboardViewModel viewModel)
            {
                viewModel.OpenFullReportCommand.Execute(null);
            }
        }

        /// <summary>
        /// Переход ко всем операциям
        /// </summary>
        private void ShowAllTransactions_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DashboardViewModel viewModel)
            {
                viewModel.NavigateToAllTransactionsCommand.Execute(null);
            }
        }

        /// <summary>
        /// Переключение видимости описания
        /// </summary>
        private void ToggleDescription_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DashboardViewModel viewModel)
            {
                viewModel.ToggleDescriptionCommand.Execute(null);
            }
        }

        /// <summary>
        /// Переключение видимости выбора даты
        /// </summary>
        private void ToggleDate_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DashboardViewModel viewModel)
            {
                viewModel.ToggleDateCommand.Execute(null);
            }
        }
    }
}
