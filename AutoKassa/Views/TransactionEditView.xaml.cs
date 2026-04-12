using AutoKassa.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoKassa.Views
{
    /// <summary>
    /// Форма добавления/редактирования операции (UserControl, встраивается в модальный оверлей)
    /// </summary>
    public partial class TransactionEditView : UserControl
    {
        public TransactionEditView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Unloaded += (_, _) => DataContextChanged -= OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is TransactionEditViewModel)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AmountTextBox?.Focus();
                    AmountTextBox?.SelectAll();
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex(@"[^0-9,.]");
            e.Handled = regex.IsMatch(e.Text);
        }

    }
}
