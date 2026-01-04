using Avalonia;
using Avalonia.Data.Converters;

namespace Froststrap.UI.Converters
{
    public class RangeConverter : IValueConverter
    {
        public int? From { get; set; }
        public int? To { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int length;

            if (value is string str)
            {
                length = str.Length;
            }
            else if (value is int intVal)
            {
                length = intVal;
            }
            else
            {
                return AvaloniaProperty.UnsetValue;
            }

            if (From is null)
                return To is null || length < To;

            if (To is null)
                return length > From;

            return length > From && length < To;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}