using AutoKassa.Models.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// MultiValueConverter: (OperationType, string CategoryColor) → SolidColorBrush
    /// Expense → #ef4444, Income → category color (or #22c55e as fallback)
    /// </summary>
    public class TransactionDotColorConverter : IMultiValueConverter
    {
        private static readonly SolidColorBrush ExpenseBrush = new(Color.FromRgb(0xef, 0x44, 0x44));
        private static readonly SolidColorBrush DefaultIncomeBrush = new(Color.FromRgb(0x22, 0xc5, 0x5e));

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is OperationType type)
            {
                if (type == OperationType.Expense)
                    return ExpenseBrush;

                if (values[1] is string hex && !string.IsNullOrWhiteSpace(hex))
                {
                    try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                    catch { }
                }
                return DefaultIncomeBrush;
            }
            return DefaultIncomeBrush;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
