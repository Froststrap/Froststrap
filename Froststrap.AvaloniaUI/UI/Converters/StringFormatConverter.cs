using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Froststrap.UI.Converters
{
    public class StringFormatConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string? valueStr = value as string;
            string? parameterStr = parameter as string;

            if (valueStr is null)
                return string.Empty;

            if (parameterStr is null)
                return valueStr;

            string[] args = parameterStr.Split(new char[] { '|' });

            return string.Format(valueStr, args);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException(nameof(ConvertBack));
        }
    }
}