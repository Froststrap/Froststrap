using System;
using System.ComponentModel;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;

namespace Froststrap.UI.Elements.Bootstrapper
{
	public partial class CustomDialog
	{
		// https://stackoverflow.com/a/2961702
		private static T? ConvertValue<T>(string input) where T : struct
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

		private static object? GetTypeFromXElement(Func<string, object> parser, XElement xmlElement, string attributeName)
		{
			string? attributeValue = xmlElement.Attribute(attributeName)?.Value?.ToString();
			if (attributeValue == null)
				return null;

			try
			{
				return parser(attributeValue);
			}
			catch (Exception ex)
			{
				throw new CustomThemeException(ex, "CustomTheme.Errors.ElementAttributeConversionError", xmlElement.Name, attributeName, ex.Message);
			}
		}

		private static object? GetThicknessFromXElement(XElement xmlElement, string attributeName)
			=> GetTypeFromXElement(s => Thickness.Parse(s), xmlElement, attributeName);

		private static object? GetRectFromXElement(XElement xmlElement, string attributeName)
			=> GetTypeFromXElement(s => Rect.Parse(s), xmlElement, attributeName);

		private static object? GetColorFromXElement(XElement xmlElement, string attributeName)
			=> GetTypeFromXElement(s => Color.Parse(s), xmlElement, attributeName);

		private static object? GetPointFromXElement(XElement xmlElement, string attributeName)
			=> GetTypeFromXElement(s => RelativePoint.Parse(s), xmlElement, attributeName);

		private static object? GetCornerRadiusFromXElement(XElement xmlElement, string attributeName)
			=> GetTypeFromXElement(s => CornerRadius.Parse(s), xmlElement, attributeName);

		private static object? GetGridLengthFromXElement(XElement xmlElement, string attributeName)
			=> GetTypeFromXElement(s => GridLength.Parse(s), xmlElement, attributeName);


		private static object? GetBrushFromXElement(XElement element, string attributeName)
		{
			string? value = element.Attribute(attributeName)?.Value?.ToString();
			if (value == null)
				return null;

			if (value.StartsWith('{') && value.EndsWith('}'))
				return value[1..^1];

			try
			{
				return Brush.Parse(value);
			}
			catch (Exception ex)
			{
				throw new CustomThemeException(ex, "CustomTheme.Errors.ElementAttributeConversionError", element.Name, attributeName, ex.Message);
			}
		}
	}
}