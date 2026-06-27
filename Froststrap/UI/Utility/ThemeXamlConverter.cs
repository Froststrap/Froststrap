using System.Xml.Linq;

namespace Froststrap.UI.Utility
{
    public static class ThemeXamlConverter
    {
        private static readonly Dictionary<string, string> PropertyRenameMap = new()
        {
            { "Panel.ZIndex", "ZIndex" }
        };

        private static readonly Dictionary<string, string> BackdropTypeMap = new()
        {
            { "Mica", "Mica" },
            { "Acrylic", "Acrylic" },
            { "Aero", "Aero" },
            { "Blur", "Aero" },
            { "None", "None" },
            { "Disable", "None" },
            { "Disabled", "None" },
            { "Default", "None" }
        };

        public static string ConvertThemeXaml(string wpfXaml)
        {
            try
            {
                var doc = XDocument.Parse(wpfXaml);
                var root = doc.Root;

                if (root == null) return wpfXaml;

                ConvertPropertiesAndValues(root);
                ConvertControlTypes(root);
                ConvertBackdropAttributes(root);

                return doc.ToString();
            }
            catch (Exception)
            {
                return wpfXaml;
            }
        }

        private static void ConvertPropertiesAndValues(XElement element)
        {
            if (element.Name.LocalName == "BloxstrapCustomBootstrapper" || element.Name.LocalName == "Window")
            {
                if (element.Attribute("WindowStyle")?.Value == "None")
                    element.SetAttributeValue("SystemDecorations", "None");

                var insetAttr = element.Attribute("IgnoreTitleBarInset");
                if (insetAttr != null)
                {
                    element.SetAttributeValue("IgnoreTitleBarInset", insetAttr.Value.ToLower());
                }

                var backgroundType = element.Attribute("BackgroundType");
                if (backgroundType != null)
                {
                    var mappedValue = BackdropTypeMap.GetValueOrDefault(backgroundType.Value, "None");
                    element.SetAttributeValue("WindowBackdropType", mappedValue);
                    backgroundType.Remove();
                }
            }

            foreach (var attr in element.Attributes().ToList())
            {
                if (PropertyRenameMap.TryGetValue(attr.Name.LocalName, out string? newName))
                {
                    element.SetAttributeValue(newName, attr.Value);
                    if (newName != attr.Name.LocalName) attr.Remove();
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

        private static void ConvertBackdropAttributes(XElement element)
        {
            if (element.Name.LocalName == "BloxstrapCustomBootstrapper" || element.Name.LocalName == "Window")
            {
                var backdropAttr = element.Attribute("WindowBackdropType");
                if (backdropAttr != null)
                {
                    var wpfValue = backdropAttr.Value;
                    if (BackdropTypeMap.TryGetValue(wpfValue, out string? avaloniaValue))
                    {
                        element.SetAttributeValue("WindowBackdropType", avaloniaValue);
                    }
                    else
                    {
                        element.SetAttributeValue("WindowBackdropType", "None");
                    }
                }
            }

            foreach (var child in element.Elements())
                ConvertBackdropAttributes(child);
        }
    }
}