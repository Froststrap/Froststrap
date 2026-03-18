using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class CustomDialog
    {
        private struct GetImageSourceDataResult
        {
            public bool IsIcon = false;
            public Uri? Uri = null;

            public GetImageSourceDataResult() { }
        }

        private static string GetXmlAttribute(XElement element, string attributeName, string? defaultValue = null)
        {
            var attribute = element.Attribute(attributeName);
            if (attribute == null)
            {
                if (defaultValue != null)
                    return defaultValue;

                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissing", element.Name.ToString(), attributeName);
            }
            return attribute.Value;
        }

        /// <summary>
        /// General parser for attributes. Handles both structs (Enums, int) and classes.
        /// </summary>
        private static T ParseXmlAttribute<T>(XElement element, string attributeName, T defaultValue)
        {
            var attribute = element.Attribute(attributeName);

            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Value))
            {
                return defaultValue;
            }

            try
            {
                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)bool.Parse(attribute.Value);
                }

                var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(T));
                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    return (T)converter.ConvertFromInvariantString(attribute.Value)!;
                }

                return (T)Convert.ChangeType(attribute.Value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Version for Nullable structs (like int?, double?)
        /// </summary>
        private static T? ParseXmlAttributeNullable<T>(XElement element, string attributeName) where T : struct
        {
            var attribute = element.Attribute(attributeName);
            if (attribute == null)
                return null;

            return ConvertValue<T>(attribute.Value);
        }

        private static void ValidateXmlElement(string elementName, string attributeName, double value, double? min = null, double? max = null)
        {
            if (min != null && value < min)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeLargerThanMin", elementName, attributeName, min);
            if (max != null && value > max)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeSmallerThanMax", elementName, attributeName, max);
        }

        private static int ParseXmlAttributeClamped(XElement element, string attributeName, int defaultValue = 0, int? min = null, int? max = null)
        {
            // Specifically call the non-nullable version
            int value = ParseXmlAttribute<int>(element, attributeName, defaultValue);
            ValidateXmlElement(element.Name.ToString(), attributeName, (double)value, min != null ? (double)min : null, max != null ? (double)max : null);
            return value;
        }

        private static FontWeight GetFontWeightFromXElement(XElement element)
        {
            string value = element.Attribute("FontWeight")?.Value ?? "Normal";
            if (Enum.TryParse<FontWeight>(value, true, out var style))
                return style;

            throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name.ToString(), "FontWeight", value);
        }

        private static FontStyle GetFontStyleFromXElement(XElement element)
        {
            string value = element.Attribute("FontStyle")?.Value ?? "Normal";
            if (Enum.TryParse<FontStyle>(value, true, out var style))
                return style;

            throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name.ToString(), "FontStyle", value);
        }

        private static TextDecorationCollection? GetTextDecorationsFromXElement(XElement element)
        {
            string? value = element.Attribute("TextDecorations")?.Value;
            if (string.IsNullOrEmpty(value))
                return null;

            if (value.Equals("Underline", StringComparison.OrdinalIgnoreCase)) return TextDecorations.Underline;
            if (value.Equals("Strikethrough", StringComparison.OrdinalIgnoreCase)) return TextDecorations.Strikethrough;

            throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name.ToString(), "TextDecorations", value);
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
            if (sourcePath == null) return null;
            return sourcePath.Replace("theme://", $"{dialog.ThemeDir}\\");
        }

        private static GetImageSourceDataResult GetImageSourceData(CustomDialog dialog, string name, XElement xmlElement)
        {
            string path = GetXmlAttribute(xmlElement, name);
            if (path == "{Icon}")
                return new GetImageSourceDataResult { IsIcon = true };

            path = GetFullPath(dialog, path)!;

            if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out Uri? result))
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeParseError", xmlElement.Name.ToString(), name, "Uri");

            return new GetImageSourceDataResult { Uri = result };
        }

        private static object? GetContentFromXElement(CustomDialog dialog, XElement xmlElement)
        {
            var contentAttr = xmlElement.Attribute("Content");
            var contentElement = xmlElement.Element($"{xmlElement.Name}.Content");

            if (contentAttr != null && contentElement != null)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMultipleDefinitions", xmlElement.Name.ToString(), "Content");

            if (contentAttr != null)
                return GetTranslatedText(contentAttr.Value);

            if (contentElement == null)
                return null;

            var children = contentElement.Elements().ToList();
            if (children.Count > 1)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMultipleChildren", xmlElement.Name.ToString(), "Content");

            var first = children.FirstOrDefault();
            if (first == null)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissingChild", xmlElement.Name.ToString(), "Content");

            return HandleXml<Control>(dialog, first);
        }

        private static void ApplyEffects_Control(CustomDialog dialog, Control uiElement, XElement xmlElement)
        {
            var effectElement = xmlElement.Element($"{xmlElement.Name}.Effect");
            if (effectElement == null) return;

            var child = effectElement.Elements().FirstOrDefault();
            if (child == null) return;

            if (child.Name.LocalName == "DropShadowEffect")
            {
                var shadow = HandleXmlElement_DropShadowEffect(dialog, child);

                if (shadow is BoxShadows bxs)
                {
                    uiElement.SetValue(Avalonia.Controls.Border.BoxShadowProperty, bxs);
                }
            }
        }

        private static void ApplyTransformations_Control(CustomDialog dialog, Control uiElement, XElement xmlElement)
        {
            var transformElement = xmlElement.Element($"{xmlElement.Name}.RenderTransform");
            if (transformElement == null) return;

            var tg = new TransformGroup();
            foreach (var child in transformElement.Elements())
            {
                var element = HandleXml<Transform>(dialog, child);
                if (element != null)
                    tg.Children.Add(element);
            }
            uiElement.RenderTransform = tg;
        }
    }
}