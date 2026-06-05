using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер HEX-строки цвета в SolidColorBrush с кешированием.
    /// Каждый цвет создаётся один раз и переиспользуется, что устраняет
    /// лишнее давление на GC при обновлении списков транзакций.
    /// </summary>
    public class CategoryColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0x94, 0xa3, 0xb8));
        private static readonly ConcurrentDictionary<string, SolidColorBrush> BrushCache = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                if (BrushCache.TryGetValue(hex, out var cached))
                    return cached;

                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    BrushCache[hex] = brush;
                    return brush;
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
