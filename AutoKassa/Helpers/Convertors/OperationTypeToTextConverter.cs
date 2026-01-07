using System;
using System.Globalization;
using System.Windows.Data;
using AutoKassa.Models.Enums;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер типа операции в текст
    /// </summary>
    public class OperationTypeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OperationType type)
            {
                return type == OperationType.Income ? "Доход" : "Расход";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}