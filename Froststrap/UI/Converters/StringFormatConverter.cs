using System.Globalization;
using Avalonia.Data.Converters;

namespace Froststrap.UI.Converters
{
    public class StringFormatConverter : IValueConverter
    {
        private static readonly char[] Separator = ['|'];

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string valueStr)
                return string.Empty;

            if (parameter is not string parameterStr)
                return valueStr;

            string[] args = parameterStr.Split(Separator);

            return string.Format(valueStr, (object[])args);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException(nameof(ConvertBack));
        }
    }
}