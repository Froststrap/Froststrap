using System;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Froststrap;

namespace Froststrap.UI.Elements.Bootstrapper
{
	public partial class CustomDialog
	{
		struct GetImageSourceDataResult
		{
			public bool IsIcon = false;
			public Uri? Uri = null;

			public GetImageSourceDataResult()
			{
			}
		}

		private static string GetXmlAttribute(XElement element, string attributeName, string? defaultValue = null)
		{
			var attribute = element.Attribute(attributeName);

			if (attribute == null)
			{
				if (defaultValue != null)
					return defaultValue;

				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissing", element.Name, attributeName);
			}

			return attribute.Value.ToString();
		}

		private static T ParseXmlAttribute<T>(XElement element, string attributeName, T? defaultValue = null) where T : struct
		{
			var attribute = element.Attribute(attributeName);

			if (attribute == null)
			{
				if (defaultValue != null)
					return (T)defaultValue;

				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissing", element.Name, attributeName);
			}

			T? parsed = ConvertValue<T>(attribute.Value);
			if (parsed == null)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeInvalidType", element.Name, attributeName, typeof(T).Name);

			return (T)parsed;
		}

		private static T? ParseXmlAttributeNullable<T>(XElement element, string attributeName) where T : struct
		{
			var attribute = element.Attribute(attributeName);

			if (attribute == null)
				return null;

			T? parsed = ConvertValue<T>(attribute.Value);
			if (parsed == null)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeInvalidType", element.Name, attributeName, typeof(T).Name);

			return (T)parsed;
		}

		private static void ValidateXmlElement(string elementName, string attributeName, int value, int? min = null, int? max = null)
		{
			if (min != null && value < min)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeLargerThanMin", elementName, attributeName, min);
			if (max != null && value > max)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeSmallerThanMax", elementName, attributeName, max);
		}

		private static void ValidateXmlElement(string elementName, string attributeName, double value, double? min = null, double? max = null)
		{
			if (min != null && value < min)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeLargerThanMin", elementName, attributeName, min);
			if (max != null && value > max)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeSmallerThanMax", elementName, attributeName, max);
		}

		private static int ParseXmlAttributeClamped(XElement element, string attributeName, int? defaultValue = null, int? min = null, int? max = null)
		{
			int value = ParseXmlAttribute<int>(element, attributeName, defaultValue);
			ValidateXmlElement(element.Name.ToString(), attributeName, value, min, max);
			return value;
		}

		private static FontWeight GetFontWeightFromXElement(XElement element)
		{
			string? value = element.Attribute("FontWeight")?.Value?.ToString();
			if (string.IsNullOrEmpty(value))
				value = "Normal";

			if (Enum.TryParse<FontWeight>(value, true, out var result))
				return result;

			return value switch
			{
				"Regular" => FontWeight.Normal,
				"UltraLight" => FontWeight.ExtraLight,
				"SemiBold" => FontWeight.SemiBold,
				"Heavy" => FontWeight.Black,
				_ => throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name, "FontWeight", value)
			};
		}

		private static FontStyle GetFontStyleFromXElement(XElement element)
		{
			string? value = element.Attribute("FontStyle")?.Value?.ToString();
			if (string.IsNullOrEmpty(value))
				value = "Normal";

			if (Enum.TryParse<FontStyle>(value, true, out var result))
				return result;

			throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name, "FontStyle", value);
		}

		private static TextDecorationCollection? GetTextDecorationsFromXElement(XElement element)
		{
			string? value = element.Attribute("TextDecorations")?.Value?.ToString();
			if (string.IsNullOrEmpty(value))
				return null;

			return value switch
			{
				"Underline" => TextDecorations.Underline,
				"Strikethrough" => TextDecorations.Strikethrough,
				_ => throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name, "TextDecorations", value)
			};
		}

		private static string? GetTranslatedText(string? text)
		{
			if (text == null || !text.StartsWith('{') || !text.EndsWith('}'))
				return text;

			string resourceName = text[1..^1];

			if (resourceName == "Version")
				return App.Version;

			return Strings.ResourceManager.GetStringSafe(resourceName);
		}

		private static string? GetFullPath(CustomDialog dialog, string? sourcePath)
		{
			if (sourcePath == null)
				return null;

			return sourcePath.Replace("theme://", $"{dialog.ThemeDir}{System.IO.Path.DirectorySeparatorChar}");
		}

		private static GetImageSourceDataResult GetImageSourceData(CustomDialog dialog, string name, XElement xmlElement)
		{
			string path = GetXmlAttribute(xmlElement, name);

			if (path == "{Icon}")
				return new GetImageSourceDataResult { IsIcon = true };

			path = GetFullPath(dialog, path)!;

			if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out Uri? result))
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeParseError", xmlElement.Name, name, "Uri");

			if (result == null)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeParseErrorNull", xmlElement.Name, name, "Uri");

			return new GetImageSourceDataResult { Uri = result };
		}

		private static object? GetContentFromXElement(CustomDialog dialog, XElement xmlElement)
		{
			var contentAttr = xmlElement.Attribute("Content");
			var contentElement = xmlElement.Element($"{xmlElement.Name}.Content");

			if (contentAttr != null && contentElement != null)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMultipleDefinitions", xmlElement.Name, "Content");

			if (contentAttr != null)
				return GetTranslatedText(contentAttr.Value);

			if (contentElement == null)
				return null;

			var children = contentElement.Elements();
			if (children.Count() > 1)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMultipleChildren", xmlElement.Name, "Content");

			var first = contentElement.Elements().FirstOrDefault();
			if (first == null)
				throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissingChild", xmlElement.Name, "Content");

			var uiElement = HandleXml<Control>(dialog, first);
			return uiElement;
		}

		private static void ApplyTransformation_Control(CustomDialog dialog, string name, AvaloniaProperty property, Control control, XElement xmlElement)
		{
			var transformElement = xmlElement.Element($"{xmlElement.Name}.{name}");

			if (transformElement == null)
				return;

			var tg = new TransformGroup();

			foreach (var child in transformElement.Elements())
			{
				Transform element = HandleXml<Transform>(dialog, child);
				tg.Children.Add(element);
			}

			control.SetValue(property, tg);
		}

		private static void ApplyTransformations_Control(CustomDialog dialog, Control control, XElement xmlElement)
		{
			ApplyTransformation_Control(dialog, "RenderTransform", Visual.RenderTransformProperty, control, xmlElement);
		}
	}
}