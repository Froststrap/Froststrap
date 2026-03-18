using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using System.Xml.Linq;

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
                throw new CustomThemeException("CustomTheme.Errors.AttributeMissing", element.Name.ToString(), attributeName);
            }
            return attribute.Value;
        }

        private static T ParseXmlAttribute<T>(XElement element, string attributeName, T? defaultValue = null) where T : struct
        {
            var attribute = element.Attribute(attributeName);
            if (attribute == null)
            {
                if (defaultValue != null) return (T)defaultValue;
                throw new CustomThemeException("CustomTheme.Errors.AttributeMissing", element.Name.ToString(), attributeName);
            }

            T? parsed = ConvertValue<T>(attribute.Value);
            if (parsed == null)
                throw new CustomThemeException("CustomTheme.Errors.AttributeInvalid", element.Name.ToString(), attributeName, typeof(T).Name);

            return (T)parsed;
        }

        private static T? ParseXmlAttributeNullable<T>(XElement element, string attributeName) where T : struct
        {
            var attribute = element.Attribute(attributeName);
            if (attribute == null) return null;

            T? parsed = ConvertValue<T>(attribute.Value);
            if (parsed == null)
                throw new Exception($"Attribute '{attributeName}' on '{element.Name}' is not a valid {typeof(T).Name}");

            return (T)parsed;
        }

        private static void ValidateXmlElement(string elementName, string attributeName, double value, double? min = null, double? max = null)
        {
            if (min != null && value < min)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeLargerThanMin", elementName, attributeName, min);

            if (max != null && value > max)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMustBeSmallerThanMax", elementName, attributeName, max);
        }

        private static FontWeight GetFontWeightFromXElement(XElement element)
        {
            string value = element.Attribute("FontWeight")?.Value ?? "Normal";
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
                _ => throw new Exception($"Unknown FontWeight '{value}'")
            };
        }

        private static FontStyle GetFontStyleFromXElement(XElement element)
        {
            string value = element.Attribute("FontStyle")?.Value ?? "Normal";
            return value switch
            {
                "Normal" => FontStyle.Normal,
                "Italic" => FontStyle.Italic,
                "Oblique" => FontStyle.Oblique,
                _ => throw new Exception($"Unknown FontStyle '{value}'")
            };
        }

        private static string? GetTranslatedText(string? text)
        {
            if (text == null || !text.StartsWith('{') || !text.EndsWith('}'))
                return text;

            string resourceName = text[1..^1];
            if (resourceName == "Version") return App.Version;

            return Strings.ResourceManager.GetString(resourceName) ?? text;
        }

        private static string? GetFullPath(CustomDialog dialog, string? sourcePath)
        {
            if (sourcePath == null) return null;
            return sourcePath.Replace("theme://", $"{dialog.ThemeDir}{Path.DirectorySeparatorChar}");
        }

        private static GetImageSourceDataResult GetImageSourceData(CustomDialog dialog, string name, XElement xmlElement)
        {
            string path = GetXmlAttribute(xmlElement, name);
            if (path == "{Icon}") return new GetImageSourceDataResult { IsIcon = true };

            string? fullPath = GetFullPath(dialog, path);
            if (string.IsNullOrEmpty(fullPath) || !Uri.TryCreate(fullPath, UriKind.RelativeOrAbsolute, out Uri? result))
                throw new CustomThemeException("CustomTheme.Errors.AttributeInvalidUri", xmlElement.Name.ToString(), name);

            return new GetImageSourceDataResult { Uri = result };
        }

        private static object? GetContentFromXElement(CustomDialog dialog, XElement xmlElement)
        {
            var contentAttr = xmlElement.Attribute("Content");
            var contentElement = xmlElement.Element($"{xmlElement.Name}.Content");

            if (contentAttr != null) return GetTranslatedText(contentAttr.Value);
            if (contentElement == null) return null;

            var first = contentElement.Elements().FirstOrDefault();
            if (first == null) return null;

            return HandleXml<Control>(dialog, first);
        }

        private static void ApplyTransformation_Control(CustomDialog dialog, string name, AvaloniaProperty property, Control control, XElement xmlElement)
        {
            var transformElement = xmlElement.Element($"{xmlElement.Name}.{name}");
            if (transformElement == null) return;

            var transformGroup = new TransformGroup();
            foreach (var child in transformElement.Elements())
            {
                var transform = HandleXml<Transform>(dialog, child);
                transformGroup.Children.Add(transform);
            }

            control.SetValue(property, transformGroup);
        }

        private static void ApplyTransformations_Control(CustomDialog dialog, Control control, XElement xmlElement)
        {
            ApplyTransformation_Control(dialog, "RenderTransform", Visual.RenderTransformProperty, control, xmlElement);
        }
    }
}