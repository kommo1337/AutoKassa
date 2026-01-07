using AutoKassa.Models;
using AutoKassa.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AutoKassa.Views
{
    /// <summary>
    /// Экран управления категориями
    /// </summary>
    public partial class CategoriesView : UserControl
    {
        public CategoriesView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Обработчик клика по кнопке редактирования
        /// </summary>
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Category category)
            {
                var viewModel = DataContext as CategoriesViewModel;
                if (viewModel != null)
                {
                    viewModel.SelectedCategory = category;
                    if (viewModel.EditCommand.CanExecute(null))
                    {
                        viewModel.EditCommand.Execute(null);
                    }
                }
            }
        }

        /// <summary>
        /// Обработчик клика по кнопке удаления
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Category category)
            {
                var viewModel = DataContext as CategoriesViewModel;
                if (viewModel != null)
                {
                    viewModel.SelectedCategory = category;
                    if (viewModel.DeleteCommand.CanExecute(null))
                    {
                        viewModel.DeleteCommand.Execute(null);
                    }
                }
            }
        }
    }
}