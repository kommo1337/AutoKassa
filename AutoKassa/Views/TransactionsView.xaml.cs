using AutoKassa.Models;
using AutoKassa.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoKassa.Views
{
    /// <summary>
    /// Экран списка операций
    /// </summary>
    public partial class TransactionsView : UserControl
    {
        public TransactionsView()
        {
            InitializeComponent();
        }

        private void Row_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is Transaction tx &&
                DataContext is TransactionsViewModel vm)
            {
                vm.SelectedTransaction = tx;

                if (e.ClickCount == 2 && vm.EditCommand.CanExecute(null))
                    vm.EditCommand.Execute(null);
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu ctx &&
                ctx.PlacementTarget is FrameworkElement el &&
                el.DataContext is Transaction tx &&
                DataContext is TransactionsViewModel vm)
            {
                vm.SelectedTransaction = tx;
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TransactionsViewModel vm && vm.EditCommand.CanExecute(null))
                vm.EditCommand.Execute(null);
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TransactionsViewModel vm && vm.DeleteCommand.CanExecute(null))
                vm.DeleteCommand.Execute(null);
        }
    }
}
