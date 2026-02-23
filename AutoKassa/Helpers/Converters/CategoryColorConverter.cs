using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер HEX-строки цвета в SolidColorBrush
    /// </summary>
    public class CategoryColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0x94, 0xa3, 0xb8));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    return DefaultBrush;
                }
            }
            return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
