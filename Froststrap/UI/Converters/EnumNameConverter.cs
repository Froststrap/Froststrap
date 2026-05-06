using System.Globalization;
using Avalonia.Data.Converters;
using Froststrap.Models.Attributes;

namespace Froststrap.UI.Converters
{
    public class EnumNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Enum enumVal)
                return value?.ToString() ?? "Unknown";

            var stringVal = enumVal.ToString();
            var type = enumVal.GetType();
            var typeName = type.FullName;

            if (string.IsNullOrEmpty(typeName))
                return stringVal;

            var memberInfo = type.GetMember(stringVal).FirstOrDefault();

            if (memberInfo?.GetCustomAttributes(typeof(EnumNameAttribute), false).FirstOrDefault() is EnumNameAttribute attribute)
            {
                if (!string.IsNullOrEmpty(attribute.StaticName))
                    return attribute.StaticName;

                if (!string.IsNullOrEmpty(attribute.FromTranslation))
                    return Strings.ResourceManager.GetStringSafe(attribute.FromTranslation);
            }

            var dotIndex = typeName.IndexOf('.');

            var trimmedTypeName = dotIndex >= 0 ? typeName[(dotIndex + 1)..] : typeName;

            return Strings.ResourceManager.GetStringSafe($"{trimmedTypeName}.{stringVal}");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}