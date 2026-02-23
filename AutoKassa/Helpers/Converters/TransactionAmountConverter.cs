using AutoKassa.Models.Enums;
using System.Globalization;
using System.Windows.Data;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// MultiValueConverter: (decimal Amount, OperationType) → "+12 222 ₽" or "−7 800 ₽"
    /// </summary>
    public class TransactionAmountConverter : IMultiValueConverter
    {
        private static readonly CultureInfo RuCulture = new("ru-RU");

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is decimal amount && values[1] is OperationType type)
            {
                var formatted = amount.ToString("N0", RuCulture);
                return type == OperationType.Income ? $"+{formatted} ₽" : $"−{formatted} ₽";
            }
            return "0 ₽";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
