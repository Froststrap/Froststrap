using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Froststrap.UI.Utility
{
    /// <summary>
    /// Converts WPF-style custom bootstrapper theme XAML to Avalonia-compatible XAML
    /// </summary>
    public static class ThemeXamlConverter
    {
        private static readonly Dictionary<string, string> WpfToAvaloniaNamespaces = new()
        {
            { "http://schemas.microsoft.com/winfx/2006/xaml/presentation", "https://github.com/avaloniaui" },
            { "http://schemas.microsoft.com/winfx/2006/xaml", "http://schemas.microsoft.com/winfx/2006/xaml" }
        };

        private static readonly Dictionary<string, string> AttachedPropertyMap = new()
        {
            { "Panel.ZIndex", "ZIndex" },
            { "Canvas.Left", "Canvas.Left" },
            { "Canvas.Top", "Canvas.Top" },
            { "Grid.Row", "Grid.Row" },
            { "Grid.Column", "Grid.Column" },
            { "Grid.RowSpan", "Grid.RowSpan" },
            { "Grid.ColumnSpan", "Grid.ColumnSpan" }
        };

        /// <summary>
        /// Converts WPF theme XAML to Avalonia-compatible XAML
        /// </summary>
        public static string ConvertThemeXaml(string wpfXaml, string themeDirectory)
        {
            try
            {
                // Parse the XAML
                var doc = XDocument.Parse(wpfXaml);
                var root = doc.Root;

                if (root == null)
                    return wpfXaml;

                // Convert namespaces
                ConvertNamespaces(root);

                // Convert theme:// URIs
                ConvertThemeUris(root, themeDirectory);

                // Convert WPF-specific properties
                ConvertWpfProperties(root);

                // Convert control types
                ConvertControlTypes(root);

                return doc.ToString();
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ThemeXamlConverter", ex);
                return wpfXaml; // Return original on error
            }
        }

        private static void ConvertNamespaces(XElement root)
        {
            // Update namespace declarations
            foreach (var ns in WpfToAvaloniaNamespaces)
            {
                var attr = root.Attribute(XNamespace.Xmlns + "");
                if (attr?.Value == ns.Key)
                {
                    attr.Value = ns.Value;
                }
            }
        }

        private static void ConvertThemeUris(XElement element, string themeDirectory)
        {
            // Process all attributes
            foreach (var attr in element.Attributes().ToList())
            {
                if (attr.Value.StartsWith("theme://", StringComparison.OrdinalIgnoreCase))
                {
                    attr.Value = ResolveThemeUri(attr.Value, themeDirectory);
                }
            }

            // Process child elements
            foreach (var child in element.Elements())
            {
                ConvertThemeUris(child, themeDirectory);
            }
        }

        private static void ConvertWpfProperties(XElement element)
        {
            // Convert FontFamily
            var fontFamilyAttr = element.Attribute("FontFamily");
            if (fontFamilyAttr != null && fontFamilyAttr.Value.StartsWith("file:///"))
            {
                // Already converted, keep as is
            }

            // Convert AllowTransparency to TransparencyLevelHint
            var allowTransparency = element.Attribute("AllowTransparency");
            if (allowTransparency != null)
            {
                allowTransparency.Remove();
                if (bool.TryParse(allowTransparency.Value, out var isTransparent) && isTransparent)
                {
                    element.Add(new XAttribute("TransparencyLevelHint", "Transparent"));
                }
            }

            // Convert WindowStyle (hide title bar if None)
            var windowStyle = element.Attribute("WindowStyle");
            if (windowStyle?.Value == "None")
            {
                element.Add(new XAttribute("SystemDecorations", "None"));
            }

            // Convert ResizeMode
            var resizeMode = element.Attribute("ResizeMode");
            if (resizeMode?.Value == "NoResize")
            {
                element.Add(new XAttribute("CanResize", "False"));
            }

            // Convert TopMost to TopLevel properties
            var topmost = element.Attribute("Topmost");
            if (topmost?.Value == "True")
            {
                element.Add(new XAttribute("Topmost", "True"));
            }

            // Process child elements
            foreach (var child in element.Elements().ToList())
            {
                ConvertWpfProperties(child);
            }
        }

        private static void ConvertControlTypes(XElement element)
        {
            // Convert Window to appropriate Avalonia control
            if (element.Name.LocalName == "Window")
            {
                // Update to use Avalonia Window
                element.Name = XName.Get("Window", "https://github.com/avaloniaui");
                
                // Add required Avalonia attributes if missing
                if (element.Attribute("Background") == null)
                {
                    element.Add(new XAttribute("Background", "{DynamicResource LayerFillColorDefaultBrush}"));
                }
            }

            // Convert TextBlock TextWrapping
            if (element.Name.LocalName == "TextBlock")
            {
                var wrappingAttr = element.Attribute("TextWrapping");
                if (wrappingAttr?.Value == "Wrap")
                {
                    wrappingAttr.Value = "Wrap";
                }
            }

            // Convert Image Source if it's a theme:// URI
            if (element.Name.LocalName == "Image")
            {
                var sourceAttr = element.Attribute("Source");
                if (sourceAttr?.Value.StartsWith("theme://") == true)
                {
                    // Already handled in ConvertThemeUris
                }
            }

            // Convert Stretch properties
            var stretchAttr = element.Attribute("Stretch");
            if (stretchAttr != null)
            {
                // Stretch values are mostly compatible, but normalize
                stretchAttr.Value = stretchAttr.Value switch
                {
                    "Fill" => "Fill",
                    "UniformToFill" => "UniformToFill",
                    "Uniform" => "Uniform",
                    "None" => "None",
                    _ => stretchAttr.Value
                };
            }

            // Convert Visibility to IsVisible
            var visibilityAttr = element.Attribute("Visibility");
            if (visibilityAttr != null)
            {
                var isVisible = visibilityAttr.Value.Equals("Visible", StringComparison.OrdinalIgnoreCase);
                element.SetAttributeValue("IsVisible", isVisible.ToString().ToLower());
                visibilityAttr.Remove();
            }

            // Process child elements
            foreach (var child in element.Elements().ToList())
            {
                ConvertControlTypes(child);
            }
        }

        private static string ResolveThemeUri(string uri, string themeDirectory)
        {
            if (!uri.StartsWith("theme://", StringComparison.OrdinalIgnoreCase))
                return uri;

            string resourcePath = uri.Substring("theme://".Length);

            // If it starts with #, it's a font reference (e.g., theme://#Mojangles)
            if (resourcePath.StartsWith("#"))
            {
                string fontName = resourcePath.Substring(1);
                
                // Look for font files with this name
                foreach (var ext in new[] { ".ttf", ".otf" })
                {
                    string fontPath = Path.Combine(themeDirectory, fontName + ext);
                    if (File.Exists(fontPath))
                    {
                        return $"file:///{fontPath.Replace("\\", "/")}";
                    }
                }

                // Fallback: return just the font name (will try system fonts)
                return fontName;
            }

            // For image and other resources, construct full path
            string fullPath = Path.Combine(themeDirectory, resourcePath);
            
            // Return as file:// URI if it exists, otherwise return relative path
            if (File.Exists(fullPath))
            {
                return $"file:///{fullPath.Replace("\\", "/")}";
            }

            // Try relative path
            return resourcePath;
        }

        /// <summary>
        /// Validates if a theme XAML can be converted
        /// </summary>
        public static bool CanConvertTheme(string xaml)
        {
            try
            {
                XDocument.Parse(xaml);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets conversion warnings/issues found during theme analysis
        /// </summary>
        public static List<string> AnalyzeThemeCompatibility(string xaml)
        {
            var issues = new List<string>();

            try
            {
                var doc = XDocument.Parse(xaml);
                var root = doc.Root;

                if (root == null)
                    return issues;

                // Check for unsupported WPF controls
                var unsupportedControls = new[] { "DockPanel", "WrapPanel", "StackPanel" };
                foreach (var control in unsupportedControls)
                {
                    if (root.Descendants(XName.Get(control)).Any())
                    {
                        issues.Add($"Control '{control}' used - may need layout adjustment");
                    }
                }

                // Check for animations
                if (root.Descendants(XName.Get("Storyboard")).Any())
                {
                    issues.Add("Animations (Storyboard) detected - not converted, may not work");
                }

                // Check for data bindings
                if (xaml.Contains("{Binding") || xaml.Contains("{TemplateBinding"))
                {
                    issues.Add("Data bindings detected - ensure ViewModels exist");
                }

                // Check for styling
                if (xaml.Contains("<Style") || xaml.Contains("{StaticResource"))
                {
                    issues.Add("Styles or StaticResources detected - may need manual adjustment");
                }
            }
            catch (Exception ex)
            {
                issues.Add($"Parse error: {ex.Message}");
            }

            return issues;
        }
    }
}
