using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AutoKassa.ViewModels;

namespace AutoKassa.Views
{
    /// <summary>
    /// Экран списка операций
    /// </summary>
    public partial class TransactionsView : UserControl
    {
        private static readonly Regex NotDigitRegex = new(@"[^0-9,.]", RegexOptions.Compiled);

        // Расстояние от низа списка, при котором догружается следующая страница
        private const double LoadMoreThreshold = 200;

        private ScrollViewer _listScrollViewer;

        public TransactionsView()
        {
            InitializeComponent();
        }

        private void InlineAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = NotDigitRegex.IsMatch(e.Text);
        }

        private void TransactionsList_Loaded(object sender, RoutedEventArgs e)
        {
            _listScrollViewer = FindInnerScrollViewer((DependencyObject)sender);
            if (_listScrollViewer != null)
                _listScrollViewer.ScrollChanged += ListScrollViewer_ScrollChanged;
        }

        private void TransactionsList_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_listScrollViewer != null)
            {
                _listScrollViewer.ScrollChanged -= ListScrollViewer_ScrollChanged;
                _listScrollViewer = null;
            }
        }

        // Infinite scroll: у низа списка — догружаем следующую страницу операций
        private void ListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_listScrollViewer == null) return;
            if (DataContext is not TransactionsViewModel vm) return;

            var nearBottom = _listScrollViewer.VerticalOffset + _listScrollViewer.ViewportHeight
                             >= _listScrollViewer.ExtentHeight - LoadMoreThreshold;
            if (nearBottom && vm.LoadMoreCommand.CanExecute(null))
                vm.LoadMoreCommand.Execute(null);
        }

        // Первый ScrollViewer визуального дерева ListBox — его основной скролл-контейнер
        private static ScrollViewer FindInnerScrollViewer(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ScrollViewer scrollViewer)
                    return scrollViewer;

                var result = FindInnerScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
