using AutoKassa.ViewModels;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace AutoKassa.Views
{
    /// <summary>
    /// Окно добавления/редактирования операции
    /// </summary>
    public partial class TransactionEditView : Window
    {
        private readonly TransactionEditViewModel _viewModel;

        public TransactionEditView(TransactionEditViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            // Подписка на события
            _viewModel.OnSaved = OnSaved;
            _viewModel.OnCancelled = OnCancelled;

            // Фокус на поле суммы
            Loaded += (s, e) =>
            {
                AmountTextBox.Focus();
                AmountTextBox.SelectAll();
            };
        }

        /// <summary>
        /// Валидация ввода только цифр и разделителей
        /// </summary>
        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры, запятую и точку
            Regex regex = new Regex(@"[^0-9,.]");
            e.Handled = regex.IsMatch(e.Text);
        }

        /// <summary>
        /// Обработка изменения текста для парсинга decimal
        /// </summary>
        private void AmountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox == null) return;

            string text = textBox.Text;

            // Если пусто, устанавливаем 0
            if (string.IsNullOrWhiteSpace(text))
            {
                _viewModel.Amount = 0;
                return;
            }

            // Заменяем точку на запятую для корректного парсинга
            text = text.Replace('.', ',');

            // Пытаемся распарсить
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal result))
            {
                _viewModel.Amount = result;
            }
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