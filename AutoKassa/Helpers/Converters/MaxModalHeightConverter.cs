using System;
using System.Globalization;
using System.Windows.Data;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Вычисляет максимальную высоту модального окна: доступная высота контейнера
    /// минус вертикальные отступы (параметр, по умолчанию 48).
    /// Модалка не превышает высоту окна даже при минимальном размере окна.
    /// </summary>
    public class MaxModalHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double available || double.IsNaN(available) || available <= 0)
                return double.PositiveInfinity;

            var margin = 48d;
            if (parameter is string s
                && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                margin = parsed;
            }

            return Math.Max(320, available - margin);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
