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
            if (e.OldValue is TransactionEditViewModel oldVm)
                oldVm.RequestFocusAmount -= OnRequestFocusAmount;

            if (e.NewValue is TransactionEditViewModel newVm)
            {
                newVm.RequestFocusAmount += OnRequestFocusAmount;
                Dispatcher.InvokeAsync(() =>
                {
                    AmountTextBox?.Focus();
                    AmountTextBox?.SelectAll();
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnRequestFocusAmount()
        {
            Dispatcher.InvokeAsync(() =>
            {
                AmountTextBox?.Focus();
                AmountTextBox?.SelectAll();
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex(@"[^0-9,.]");
            e.Handled = regex.IsMatch(e.Text);
        }

        /// <summary>
        /// Tab из поля суммы перемещает фокус сразу на описание
        /// </summary>
        private void AmountTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                DescriptionTextBox?.Focus();
            }
        }

        /// <summary>
        /// Enter подтверждает закрытие формы при показанном тосте "Отменить изменения?"
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None &&
                DataContext is TransactionEditViewModel vm && vm.IsCancelToastVisible)
            {
                e.Handled = true;
                vm.ConfirmCancelCommand.Execute(null);
            }
        }

    }
}
