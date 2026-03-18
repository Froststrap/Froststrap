using System.ComponentModel;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class CustomDialog
    {
        private static T? ConvertValue<T>(string input)
        {
            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (converter != null)
                {
                    return (T?)converter.ConvertFromInvariantString(input);
                }
                return default;
            }
            catch (NotSupportedException)
            {
                return default;
            }
        }

        private static object? GetValueFromXElement<T>(XElement xmlElement, string attributeName, Func<string, T> parser)
        {
            string? attributeValue = xmlElement.Attribute(attributeName)?.Value;
            if (string.IsNullOrEmpty(attributeValue)) return null;

            try
            {
                return parser(attributeValue);
            }
            catch (Exception ex)
            {
                throw new CustomThemeException(ex, "CustomTheme.Errors.AttributeConversionFailed", xmlElement.Name.ToString(), attributeName);
            }
        }

        private static object? GetThicknessFromXElement(XElement xmlElement, string attributeName)
            => GetValueFromXElement(xmlElement, attributeName, Thickness.Parse);

        private static object? GetRectFromXElement(XElement xmlElement, string attributeName)
            => GetValueFromXElement(xmlElement, attributeName, Rect.Parse);

        private static object? GetColorFromXElement(XElement xmlElement, string attributeName)
            => GetValueFromXElement(xmlElement, attributeName, Color.Parse);

        private static object? GetPointFromXElement(XElement xmlElement, string attributeName)
            => GetValueFromXElement(xmlElement, attributeName, Point.Parse);

        private static object? GetCornerRadiusFromXElement(XElement xmlElement, string attributeName)
            => GetValueFromXElement(xmlElement, attributeName, CornerRadius.Parse);

        private static object? GetGridLengthFromXElement(XElement xmlElement, string attributeName)
            => GetValueFromXElement(xmlElement, attributeName, GridLength.Parse);

        private static object? GetBrushFromXElement(XElement element, string attributeName)
        {
            string? value = element.Attribute(attributeName)?.Value;
            if (string.IsNullOrEmpty(value)) return null;

            if (value.StartsWith('{') && value.EndsWith('}'))
                return value[1..^1];

            try
            {
                return Brush.Parse(value);
            }
            catch (Exception ex)
            {
                throw new CustomThemeException(ex, "CustomTheme.Errors.AttributeConversionFailed", element.Name.ToString(), attributeName);
            }
        }
    }
}