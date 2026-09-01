using Avalonia.Data.Converters;

namespace Froststrap.UI.Converters
{
    internal class EnumNameConverter : IValueConverter
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
                    return Strings.ResourceManager.GetString(attribute.FromTranslation, CultureInfo.CurrentCulture) ?? attribute.FromTranslation;
            }

            var dotIndex = typeName.IndexOf('.', StringComparison.Ordinal);

            var trimmedTypeName = dotIndex >= 0 ? typeName[(dotIndex + 1)..] : typeName;

            return Strings.ResourceManager.GetString($"{trimmedTypeName}.{stringVal}", CultureInfo.CurrentCulture) ?? $"{trimmedTypeName}.{stringVal}";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}