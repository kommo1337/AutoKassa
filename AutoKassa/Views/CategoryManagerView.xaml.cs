using AutoKassa.Models.Enums;
using AutoKassa.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AutoKassa.Views
{
    public partial class CategoryManagerView : UserControl
    {
        private Point _dragStartPoint;
        private bool _isDragging;

        public CategoryManagerView()
        {
            InitializeComponent();
        }

        // ===== Drag & Drop =====

        private void Item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void Item_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var diff = _dragStartPoint - e.GetPosition(null);
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (sender is Border border && border.DataContext is CategoryItemViewModel item && !item.IsRenaming)
            {
                _isDragging = true;
                DragDrop.DoDragDrop(border, item, DragDropEffects.Move);
                _isDragging = false;
            }
        }

        private void Item_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(CategoryItemViewModel)))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private async void Item_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(CategoryItemViewModel))) return;

            var source = (CategoryItemViewModel)e.Data.GetData(typeof(CategoryItemViewModel));
            if (sender is Border border && border.DataContext is CategoryItemViewModel target && source != target)
            {
                var vm = DataContext as CategoryManagerViewModel;
                if (vm != null)
                {
                    await vm.MoveCategoryAsync(source, target, source.Model.Type);
                }
            }
        }

        // ===== Double-click rename =====

        private void Name_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBlock tb && tb.DataContext is CategoryItemViewModel item)
            {
                item.StartRenameCommand.Execute(null);
                e.Handled = true;
            }
        }

        // ===== Rename TextBox =====

        private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is CategoryItemViewModel item && item.IsRenaming)
            {
                item.CommitRenameCommand.Execute(null);
            }
        }

        private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is CategoryItemViewModel item)
            {
                if (e.Key == Key.Enter)
                {
                    item.CommitRenameCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    item.CancelRenameCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void RenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox tb && (bool)e.NewValue)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                {
                    tb.Focus();
                    tb.SelectAll();
                });
            }
        }

        // ===== Color swatch click =====

        private void ColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string hex)
            {
                // Walk up visual tree to find the CategoryItemViewModel
                var parent = FindAncestorDataContext<CategoryItemViewModel>(btn);
                if (parent != null)
                {
                    parent.SelectColorCommand.Execute(hex);
                }
            }
        }

        private static T FindAncestorDataContext<T>(DependencyObject child) where T : class
        {
            var current = child;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is T dc)
                    return dc;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
