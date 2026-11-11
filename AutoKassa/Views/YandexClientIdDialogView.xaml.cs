using System.Windows.Controls;

namespace AutoKassa.Views
{
    /// <summary>
    /// Модальный диалог ввода ClientId Яндекс Диска: показывается оверлеем внутри
    /// окна настроек (паттерн редактора операции), DataContext — YandexClientIdDialogViewModel.
    /// </summary>
    public partial class YandexClientIdDialogView : UserControl
    {
        public YandexClientIdDialogView()
        {
            InitializeComponent();
        }
    }
}
