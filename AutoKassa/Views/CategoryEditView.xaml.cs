using AutoKassa.ViewModels;
using System.Windows;

namespace AutoKassa.Views
{
    /// <summary>
    /// Окно добавления/редактирования категории
    /// </summary>
    public partial class CategoryEditView : Window
    {
        private readonly CategoryEditViewModel _viewModel;

        public CategoryEditView(CategoryEditViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            // Подписка на события
            _viewModel.OnSaved = OnSaved;
            _viewModel.OnCancelled = OnCancelled;

            // Фокус на поле названия
            Loaded += (s, e) =>
            {
                NameTextBox.Focus();
                NameTextBox.SelectAll();
            };
        }

        private void OnSaved()
        {
            DialogResult = true;
            Close();
        }

        private void OnCancelled()
        {
            DialogResult = false;
            Close();
        }
    }
}