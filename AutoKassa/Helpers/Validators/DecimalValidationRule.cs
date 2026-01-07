using System.Globalization;
using System.Windows.Controls;

namespace AutoKassa.Helpers.Validators
{
    /// <summary>
    /// Правило валидации для decimal значений
    /// </summary>
    public class DecimalValidationRule : ValidationRule
    {
        public decimal MinValue { get; set; } = 0.01m;
        public decimal MaxValue { get; set; } = 999999999m;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult(false, "Введите сумму");
            }

            string strValue = value.ToString().Replace(" ", "").Replace("₽", "");

            if (!decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal result))
            {
                return new ValidationResult(false, "Неверный формат числа");
            }

            if (result < MinValue)
            {
                return new ValidationResult(false, $"Сумма должна быть не менее {MinValue}");
            }

            if (result > MaxValue)
            {
                return new ValidationResult(false, $"Сумма должна быть не более {MaxValue}");
            }

            return ValidationResult.ValidResult;
        }
    }
}