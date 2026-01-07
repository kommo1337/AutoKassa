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
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}