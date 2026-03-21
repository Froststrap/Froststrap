using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Froststrap.UI.Converters
{
    public class EnumToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null) return true;
            return value?.Equals(parameter) ?? false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.Equals(true) == true ? parameter : BindingOperations.DoNothing;
        }
    }
}