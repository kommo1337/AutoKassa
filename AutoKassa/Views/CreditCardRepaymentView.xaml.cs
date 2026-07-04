using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoKassa.Views
{
    /// <summary>
    /// Interaction logic for CreditCardRepaymentView.xaml
    /// </summary>
    public partial class CreditCardRepaymentView : UserControl
    {
        private static readonly Regex _amountRegex = new("^[0-9]*[,.]?[0-9]*$");

        public CreditCardRepaymentView()
        {
            InitializeComponent();
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = (TextBox)sender;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !_amountRegex.IsMatch(newText);
        }
    }
}
