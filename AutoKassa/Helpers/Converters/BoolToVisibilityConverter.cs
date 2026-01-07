using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AutoKassa.Helpers.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = false;

            if (value is bool b)
            {
                boolValue = b;
            }
            else if (value is string str && !string.IsNullOrEmpty(str))
            {
                boolValue = true;
            }
            else if (value != null)
            {
                boolValue = true;
            }

            var inverse = parameter?.ToString()?.ToLower() == "inverse";

            if (inverse)
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }

            return false;
        }
    }
}