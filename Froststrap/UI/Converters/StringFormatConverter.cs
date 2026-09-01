using Avalonia.Data.Converters;

namespace Froststrap.UI.Converters
{
    internal class StringFormatConverter : IValueConverter
    {
        private static readonly char[] Separator = ['|'];

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string valueStr)
                return string.Empty;

            if (parameter is not string parameterStr)
                return valueStr;

            string[] args = parameterStr.Split(Separator);

            return string.Format(CultureInfo.CurrentCulture, valueStr, (object[])args);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException(nameof(ConvertBack));
        }
    }
}