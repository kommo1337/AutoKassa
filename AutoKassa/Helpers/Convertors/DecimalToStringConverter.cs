using System;
using System.Globalization;
using System.Windows.Data;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер decimal в строку с форматированием
    /// </summary>
    public class DecimalToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue.ToString("N2", CultureInfo.CurrentCulture) + " ₽";
            }
            return "0,00 ₽";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                stringValue = stringValue.Replace("₽", "").Replace(" ", "").Trim();
                if (decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal result))
                {
                    return result;
                }
            }
            return 0m;
        }
    }
}