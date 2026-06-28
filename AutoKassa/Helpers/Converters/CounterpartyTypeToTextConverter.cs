using System;
using System.Globalization;
using System.Windows.Data;
using AutoKassa.Models.Enums;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер типа контрагента в текст
    /// </summary>
    public class CounterpartyTypeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CounterpartyType type)
            {
                return type switch
                {
                    CounterpartyType.Client => "Клиент",
                    CounterpartyType.Branch => "Филиал",
                    CounterpartyType.Supplier => "Поставщик",
                    CounterpartyType.Other => "Прочее",
                    _ => "Неизвестно"
                };
            }
            return "Неизвестно";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
