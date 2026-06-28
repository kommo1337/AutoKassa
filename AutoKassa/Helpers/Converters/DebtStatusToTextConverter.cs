using System;
using System.Globalization;
using System.Windows.Data;
using AutoKassa.Models.Enums;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер статуса долга в текст
    /// </summary>
    public class DebtStatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DebtStatus status)
            {
                return status switch
                {
                    DebtStatus.NotDebt => "—",
                    DebtStatus.Active => "Активен",
                    DebtStatus.Repaid => "Погашён",
                    DebtStatus.WrittenOff => "Списан",
                    _ => "Неизвестно"
                };
            }
            return "Неизвестно";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
