using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoKassa.Views
{
    /// <summary>
    /// Экран списка операций
    /// </summary>
    public partial class TransactionsView : UserControl
    {
        private static readonly Regex NotDigitRegex = new(@"[^0-9,.]", RegexOptions.Compiled);

        public TransactionsView()
        {
            InitializeComponent();
        }

        private void InlineAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = NotDigitRegex.IsMatch(e.Text);
        }
    }
}
