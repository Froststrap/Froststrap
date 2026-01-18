using Avalonia;
using Avalonia.Data.Converters;

namespace Froststrap.UI.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool result = boolValue;

                if (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
                {
                    result = !result;
                }

                return result;
            }

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue;

            return AvaloniaProperty.UnsetValue;
        }
    }
}