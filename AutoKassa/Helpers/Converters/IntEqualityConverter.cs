using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AutoKassa.Helpers.Converters
{
    public class IntEqualityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter is string paramStr && int.TryParse(paramStr, out var paramInt))
                return intValue == paramInt;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is true && parameter is string paramStr && int.TryParse(paramStr, out var paramInt))
                return paramInt;
            return Binding.DoNothing;
        }
    }
}
