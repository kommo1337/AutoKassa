using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер строки в Visibility
    /// Возвращает Visible если строка не пустая, Collapsed если пустая
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
