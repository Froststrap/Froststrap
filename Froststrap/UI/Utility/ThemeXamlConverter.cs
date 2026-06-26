using System.Xml.Linq;

namespace Froststrap.UI.Utility
{
    public static class ThemeXamlConverter
    {
        private static readonly Dictionary<string, string> PropertyRenameMap = new()
        {
            { "Panel.ZIndex", "ZIndex" }
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

                if (element.Attribute("AllowTransparency")?.Value?.ToLower() == "true")
                    element.SetAttributeValue("TransparencyLevelHint", "Transparent");

                var insetAttr = element.Attribute("IgnoreTitleBarInset");
                if (insetAttr != null)
                {
                    element.SetAttributeValue("IgnoreTitleBarInset", insetAttr.Value.ToLower());
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
    }
}