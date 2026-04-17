using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class CustomDialog
    {
        struct GetImageSourceDataResult
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
                if (defaultValue != null) return defaultValue;
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissing", element.Name.LocalName, attributeName);
            }
            return attribute.Value;
        }

        private static T ParseXmlAttribute<T>(XElement element, string attributeName, T? defaultValue = null) where T : struct
        {
            var attribute = element.Attribute(attributeName);
            if (attribute == null)
            {
                if (defaultValue != null) return (T)defaultValue;
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissing", element.Name.LocalName, attributeName);
            }

            T? parsed = ConvertValue<T>(attribute.Value);
            if (parsed == null)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeInvalidType", element.Name.LocalName, attributeName, typeof(T).Name);

            return (T)parsed;
        }

        private static T? ParseXmlAttributeNullable<T>(XElement element, string attributeName) where T : struct
        {
            var attribute = element.Attribute(attributeName);
            if (attribute == null) return null;

            T? parsed = ConvertValue<T>(attribute.Value);
            if (parsed == null)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeInvalidType", element.Name.LocalName, attributeName, typeof(T).Name);

            return (T)parsed;
        }

        private static void ValidateXmlElement(string elementName, string attributeName, int value, int? min = null, int? max = null)
        {
            if (min != null && value < min) throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeLargerThanMin", elementName, attributeName, min);
            if (max != null && value > max) throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeSmallerThanMax", elementName, attributeName, max);
        }

        private static int ParseXmlAttributeClamped(XElement element, string attributeName, int? defaultValue = null, int? min = null, int? max = null)
        {
            int value = ParseXmlAttribute<int>(element, attributeName, defaultValue);
            ValidateXmlElement(element.Name.LocalName, attributeName, value, min, max);
            return value;
        }

        private static FontWeight GetFontWeightFromXElement(XElement element)
        {
            string? value = element.Attribute("FontWeight")?.Value;
            if (string.IsNullOrEmpty(value)) value = "Normal";

            return value switch
            {
                "Thin" => FontWeight.Thin,
                "ExtraLight" or "UltraLight" => FontWeight.ExtraLight,
                "Light" => FontWeight.Light,
                "Medium" => FontWeight.Medium,
                "Normal" or "Regular" => FontWeight.Normal,
                "DemiBold" or "SemiBold" => FontWeight.SemiBold,
                "Bold" => FontWeight.Bold,
                "ExtraBold" or "UltraBold" => FontWeight.ExtraBold,
                "Black" or "Heavy" => FontWeight.Black,
                "ExtraBlack" or "UltraBlack" => FontWeight.UltraBlack,
                _ => throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name.LocalName, "FontWeight", value)
            };
        }

        private static FontStyle GetFontStyleFromXElement(XElement element)
        {
            string? value = element.Attribute("FontStyle")?.Value;
            if (string.IsNullOrEmpty(value)) value = "Normal";

            return value switch
            {
                "Normal" => FontStyle.Normal,
                "Italic" => FontStyle.Italic,
                "Oblique" => FontStyle.Oblique,
                _ => throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name.LocalName, "FontStyle", value)
            };
        }

        private static TextDecorationCollection? GetTextDecorationsFromXElement(XElement element)
        {
            string? value = element.Attribute("TextDecorations")?.Value;
            if (string.IsNullOrEmpty(value)) return null;

            return value switch
            {
                "Underline" => TextDecorations.Underline,
                "Strikethrough" => TextDecorations.Strikethrough,
                "Baseline" => null,
                "Overline" => null,
                _ => throw new CustomThemeException("CustomTheme.Errors.UnknownEnumValue", element.Name.LocalName, "TextDecorations", value)
            };
        }

        private static TextTrimming GetTextTrimmingFromXElement(XElement element)
        {
            string? value = element.Attribute("TextTrimming")?.Value;
            if (string.IsNullOrEmpty(value)) value = "None";

            return value switch
            {
                "CharacterEllipsis" => TextTrimming.CharacterEllipsis,
                "WordEllipsis" => TextTrimming.WordEllipsis,
                "None" => TextTrimming.None,
                _ => TextTrimming.None
            };
        }

        private static string? GetTranslatedText(string? text)
        {
            if (text == null || !text.StartsWith('{') || !text.EndsWith('}'))
                return text;

            string resourceName = text[1..^1];
            if (resourceName == "Version") return App.Version;

            return Strings.ResourceManager.GetStringSafe(resourceName);
        }

        private static string? GetFullPath(CustomDialog dialog, string? sourcePath)
        {
            if (sourcePath == null) return null;
            return sourcePath.Replace("theme://", $"{dialog.ThemeDir}{System.IO.Path.DirectorySeparatorChar}");
        }

        private static GetImageSourceDataResult GetImageSourceData(CustomDialog dialog, string name, XElement xmlElement)
        {
            string path = GetXmlAttribute(xmlElement, name);
            if (path == "{Icon}") return new GetImageSourceDataResult { IsIcon = true };

            path = GetFullPath(dialog, path)!;

            if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out Uri? result))
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeParseError", xmlElement.Name.LocalName, name, "Uri");

            if (result != null && result.IsAbsoluteUri && result.Scheme != "file" && result.Scheme != "avares")
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeBlacklistedUriScheme", xmlElement.Name.LocalName, name, result.Scheme);

            return new GetImageSourceDataResult { Uri = result };
        }

        private static object? GetContentFromXElement(CustomDialog dialog, XElement xmlElement)
        {
            var contentAttr = xmlElement.Attribute("Content");
            var contentElement = xmlElement.Element($"{xmlElement.Name.LocalName}.Content");

            if (contentAttr != null && contentElement != null)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMultipleDefinitions", xmlElement.Name.LocalName, "Content");

            if (contentAttr != null) return GetTranslatedText(contentAttr.Value);
            if (contentElement == null) return null;

            var firstChild = contentElement.Elements().FirstOrDefault();
            if (firstChild == null) return null;

            return HandleXml<Control>(dialog, firstChild);
        }

        private static void ApplyEffects_UIElement(CustomDialog dialog, Control uiElement, XElement xmlElement)
        {
            var effectElement = xmlElement.Element($"{xmlElement.Name.LocalName}.Effect");
            if (effectElement == null) return;

            var child = effectElement.Elements().FirstOrDefault();
            if (child == null) return;

            var effect = HandleXml<IEffect>(dialog, child);
            uiElement.Effect = effect;
        }

        private static void ApplyTransformation_UIElement(CustomDialog dialog, string name, AvaloniaProperty property, Control uiElement, XElement xmlElement)
        {
            var transformElement = xmlElement.Element($"{xmlElement.Name.LocalName}.{name}");
            if (transformElement == null) return;

            var tg = new TransformGroup();
            foreach (var child in transformElement.Elements())
            {
                Transform element = HandleXml<Transform>(dialog, child);
                tg.Children.Add(element);
            }

            uiElement.SetValue(property, tg);
        }

        private static void ApplyTransformations_UIElement(CustomDialog dialog, Control uiElement, XElement xmlElement)
        {
            ApplyTransformation_UIElement(dialog, "RenderTransform", Visual.RenderTransformProperty, uiElement, xmlElement);
        }
    }
}