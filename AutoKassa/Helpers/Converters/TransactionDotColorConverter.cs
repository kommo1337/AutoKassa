using AutoKassa.Models.Enums;
using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// MultiValueConverter: (OperationType, string CategoryColor) → SolidColorBrush с кешированием.
    /// Каждая кисть создаётся один раз и замораживается (Freeze) для безопасного использования
    /// из любого потока без пересоздания при каждом обновлении списка.
    /// </summary>
    public class TransactionDotColorConverter : IMultiValueConverter
    {
        private static readonly SolidColorBrush ExpenseBrush;
        private static readonly SolidColorBrush DefaultIncomeBrush;
        private static readonly ConcurrentDictionary<string, SolidColorBrush> IncomeBrushCache = new();

        static TransactionDotColorConverter()
        {
            ExpenseBrush = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            ExpenseBrush.Freeze();
            DefaultIncomeBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            DefaultIncomeBrush.Freeze();
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is OperationType type)
            {
                if (type == OperationType.Expense)
                    return ExpenseBrush;

                if (values[1] is string hex && !string.IsNullOrWhiteSpace(hex))
                {
                    if (IncomeBrushCache.TryGetValue(hex, out var cached))
                        return cached;

                    try
                    {
                        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                        brush.Freeze();
                        IncomeBrushCache[hex] = brush;
                        return brush;
                    }
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
