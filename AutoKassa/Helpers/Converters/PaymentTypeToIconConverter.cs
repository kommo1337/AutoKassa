using AutoKassa.Models.Enums;
using System.Globalization;
using System.Windows.Data;

namespace AutoKassa.Helpers.Converters
{
    /// <summary>
    /// Конвертер типа оплаты в иконку (эмодзи)
    /// </summary>
    public class PaymentTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PaymentType paymentType)
            {
                return paymentType switch
                {
                    PaymentType.Cash => "💵",
                    PaymentType.NonCash => "💳",
                    PaymentType.CreditCard => "💳",
                    _ => "💵"
                };
            }
            return "💵";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
