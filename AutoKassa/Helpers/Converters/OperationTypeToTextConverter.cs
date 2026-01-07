using System;
using System.Globalization;
using System.Windows.Data;
using AutoKassa.Models.Enums;

namespace AutoKassa.Helpers.Converters
{
    public class OperationTypeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OperationType operationType)
            {
                return operationType == OperationType.Income ? "Доход" : "Расход";
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}