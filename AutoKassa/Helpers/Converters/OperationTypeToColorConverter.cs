using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AutoKassa.Models.Enums;

namespace AutoKassa.Helpers.Converters
{
    public class OperationTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OperationType operationType)
            {
                return operationType == OperationType.Income
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22c55e"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444"));
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}