using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AutoKassa.Models.Enums;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер типа операции в цвет
    /// </summary>
    public class OperationTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OperationType type)
            {
                return type == OperationType.Income
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // Зеленый для доходов
                    : new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Красный для расходов
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}