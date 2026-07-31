using System.Windows.Controls;

namespace AutoKassa.Views.Reports
{
    public partial class BalanceReportView : UserControl
    {
        public BalanceReportView()
        {
            InitializeComponent();
            Unloaded += (_, _) => Plot.Model = null;
        }
    }
}
