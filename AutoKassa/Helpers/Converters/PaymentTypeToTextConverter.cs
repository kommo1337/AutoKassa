using AutoKassa.Models.Enums;
using System.Globalization;
using System.Windows.Data;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер типа оплаты в текст
    /// </summary>
    public class PaymentTypeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PaymentType paymentType)
            {
                return paymentType switch
                {
                    PaymentType.Cash => "Наличные",
                    PaymentType.NonCash => "Безналичные",
                    PaymentType.CreditCard => "Кредит",
                    _ => "Наличные"
                };
            }
            return "Наличные";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
