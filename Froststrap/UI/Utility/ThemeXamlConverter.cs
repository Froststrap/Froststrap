using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Froststrap.UI.Utility
{
    public static class ThemeXamlConverter
    {
        private static readonly Dictionary<string, string> WpfToAvaloniaNamespaces = new()
        {
            { "http://schemas.microsoft.com/winfx/2006/xaml/presentation", "https://github.com/avaloniaui" },
            { "http://schemas.microsoft.com/winfx/2006/xaml", "http://schemas.microsoft.com/winfx/2006/xaml" }
        };

        private static readonly Dictionary<string, string> PropertyRenameMap = new()
        {
            { "Panel.ZIndex", "ZIndex" },
            { "Focusable", "Focusable" },
            { "IsHitTestVisible", "IsHitTestVisible" }
        };

        public static string ConvertThemeXaml(string wpfXaml, string themeDirectory)
        {
            try
            {
                var doc = XDocument.Parse(wpfXaml);
                var root = doc.Root;

                if (root == null) return wpfXaml;

                ConvertNamespaces(root);
                ConvertThemeUris(root, themeDirectory);
                ConvertPropertiesAndValues(root);
                ConvertControlTypes(root);

                return doc.ToString();
            }
            catch (Exception)
            {
                return wpfXaml;
            }
        }

        private static void ConvertNamespaces(XElement root)
        {
            foreach (var attr in root.Attributes().Where(a => a.IsNamespaceDeclaration))
            {
                if (WpfToAvaloniaNamespaces.TryGetValue(attr.Value, out string? avaloniaNs))
                {
                    attr.Value = avaloniaNs;
                }
            }
        }

        private static void ConvertPropertiesAndValues(XElement element)
        {
            if (element.Name.LocalName == "TitleBar")
            {
                var visibilityAttr = element.Attribute("Visibility");
                if (visibilityAttr != null)
                {
                    bool isVisible = visibilityAttr.Value.Equals("Visible", StringComparison.OrdinalIgnoreCase);
                    element.SetAttributeValue("IsVisible", isVisible.ToString().ToLower());
                    visibilityAttr.Remove();
                }
            }

            foreach (var attr in element.Attributes().ToList())
            {
                if (PropertyRenameMap.TryGetValue(attr.Name.LocalName, out string? newName))
                {
                    element.SetAttributeValue(newName, attr.Value);
                    if (newName != attr.Name.LocalName) attr.Remove();
                }

                if (attr.Name.LocalName == "Visibility")
                {
                    bool isVisible = attr.Value.Equals("Visible", StringComparison.OrdinalIgnoreCase);
                    element.SetAttributeValue("IsVisible", isVisible.ToString().ToLower());
                    attr.Remove();
                }

                if (attr.Value.Contains("SystemParameters.") || attr.Value.Contains("DynamicResource Window"))
                {
                    attr.Value = "{DynamicResource ApplicationPageBackgroundThemeBrush}";
                }
            }

            if (element.Name.LocalName == "BloxstrapCustomBootstrapper" || element.Name.LocalName == "Window")
            {
                if (element.Attribute("WindowStyle")?.Value == "None")
                    element.SetAttributeValue("SystemDecorations", "None");

                if (element.Attribute("AllowTransparency")?.Value?.ToLower() == "true")
                    element.SetAttributeValue("TransparencyLevelHint", "Transparent");

                var insetAttr = element.Attribute("IgnoreTitleBarInset");
                if (insetAttr != null)
                {
                    element.SetAttributeValue("IgnoreTitleBarInset", insetAttr.Value.ToLower());
                }
            }

            foreach (var child in element.Elements())
                ConvertPropertiesAndValues(child);
        }

        private static void ConvertControlTypes(XElement element)
        {
            if (element.Name.LocalName == "Label")
            {
                element.Name = element.Name.Namespace + "TextBlock";
            }

            if (element.Name.LocalName == "Window")
            {
                element.Name = element.Name.Namespace + "BloxstrapCustomBootstrapper";
            }

            foreach (var child in element.Elements())
                ConvertControlTypes(child);
        }

        private static void ConvertThemeUris(XElement element, string themeDirectory)
        {
            foreach (var attr in element.Attributes().ToList())
            {
                if (attr.Value.StartsWith("theme://", StringComparison.OrdinalIgnoreCase))
                {
                    attr.Value = ResolveThemeUri(attr.Value, themeDirectory);
                }
            }

            foreach (var child in element.Elements())
                ConvertThemeUris(child, themeDirectory);
        }

        private static string ResolveThemeUri(string uri, string themeDirectory)
        {
            if (!uri.StartsWith("theme://", StringComparison.OrdinalIgnoreCase))
                return uri;

            string resourcePath = uri.Substring("theme://".Length);

            if (resourcePath.StartsWith("#"))
                return uri;

            string fullPath = Path.Combine(themeDirectory, resourcePath);
            if (File.Exists(fullPath))
                return $"file:///{fullPath.Replace("\\", "/")}";

            return resourcePath;
        }
    }
}