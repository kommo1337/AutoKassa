using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoKassa.Views
{
    /// <summary>
    /// Interaction logic for DebtRepaymentView.xaml
    /// </summary>
    public partial class DebtRepaymentView : UserControl
    {
        private static readonly Regex _amountInputRegex = new Regex(@"[^0-9,.]");

        public DebtRepaymentView()
        {
            InitializeComponent();

            // Ограничиваем вставку в поле суммы только допустимыми символами
            DataObject.AddPastingHandler(AmountTextBox, AmountTextBox_Paste);
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _amountInputRegex.IsMatch(e.Text);
        }

        private void AmountTextBox_Paste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.Text, true))
                return;

            var text = e.SourceDataObject.GetData(DataFormats.Text, true) as string;
            if (text != null && _amountInputRegex.IsMatch(text))
                e.CancelCommand();
        }
    }
}
