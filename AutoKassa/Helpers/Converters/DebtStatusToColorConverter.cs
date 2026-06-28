using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AutoKassa.Models.Enums;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер статуса долга в цвет
    /// </summary>
    public class DebtStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DebtStatus status)
            {
                return status switch
                {
                    DebtStatus.Active => new SolidColorBrush(Colors.Orange),
                    DebtStatus.Repaid => new SolidColorBrush(Colors.Green),
                    DebtStatus.WrittenOff => new SolidColorBrush(Colors.Gray),
                    _ => new SolidColorBrush(Colors.Black)
                };
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
