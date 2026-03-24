using Avalonia.Controls;
using System.Xml.Linq;
using Froststrap.UI.Utility;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class CustomDialog
    {
        const int Version = 1;
        private class DummyControl : Control { }
        private const int MaxElements = 100;
        private bool _initialised = false;
        private string ThemeDir { get; set; } = "";

        delegate object HandleXmlElementDelegate(CustomDialog dialog, XElement xmlElement);

        private static Dictionary<string, HandleXmlElementDelegate> _elementHandlerMap = new Dictionary<string, HandleXmlElementDelegate>()
        {
            ["BloxstrapCustomBootstrapper"] = HandleXmlElement_BloxstrapCustomBootstrapper_Fake,
            ["TitleBar"] = HandleXmlElement_TitleBar,
            ["Button"] = HandleXmlElement_Button,
            ["ProgressBar"] = HandleXmlElement_ProgressBar,
            ["ProgressRing"] = HandleXmlElement_ProgressRing,
            ["TextBlock"] = HandleXmlElement_TextBlock,
            ["MarkdownTextBlock"] = HandleXmlElement_MarkdownTextBlock,
            ["Image"] = HandleXmlElement_Image,
            ["Grid"] = HandleXmlElement_Grid,
            ["StackPanel"] = HandleXmlElement_StackPanel,
            ["Border"] = HandleXmlElement_Border,
            ["SolidColorBrush"] = HandleXmlElement_SolidColorBrush,
            ["ImageBrush"] = HandleXmlElement_ImageBrush,
            ["LinearGradientBrush"] = HandleXmlElement_LinearGradientBrush,
            ["GradientStop"] = HandleXmlElement_GradientStop,
            ["ScaleTransform"] = HandleXmlElement_ScaleTransform,
            ["SkewTransform"] = HandleXmlElement_SkewTransform,
            ["RotateTransform"] = HandleXmlElement_RotateTransform,
            ["TranslateTransform"] = HandleXmlElement_TranslateTransform,
            ["DropShadowEffect"] = HandleXmlElement_DropShadowEffect,
            ["Ellipse"] = HandleXmlElement_Ellipse,
            ["Line"] = HandleXmlElement_Line,
            ["Rectangle"] = HandleXmlElement_Rectangle,
            ["RowDefinition"] = HandleXmlElement_RowDefinition,
            ["ColumnDefinition"] = HandleXmlElement_ColumnDefinition
        };

        private static T HandleXml<T>(CustomDialog dialog, XElement xmlElement) where T : class
        {
            if (!_elementHandlerMap.ContainsKey(xmlElement.Name.ToString()))
                throw new CustomThemeException("CustomTheme.Errors.ElementUnknown", xmlElement.Name.ToString());

            var element = _elementHandlerMap[xmlElement.Name.ToString()](dialog, xmlElement);
            if (element is not T)
                throw new CustomThemeException("CustomTheme.Errors.ElementTypeMismatch", xmlElement.Name.ToString(), typeof(T).Name);

            return (T)element;
        }

        private static void AssertThemeVersion(string? versionStr)
        {
            if (string.IsNullOrEmpty(versionStr))
                throw new CustomThemeException("CustomTheme.Errors.VersionMissing");

            if (!uint.TryParse(versionStr, out uint version))
                throw new CustomThemeException("CustomTheme.Errors.VersionInvalid");

            if (version != Version)
                throw new CustomThemeException("CustomTheme.Errors.VersionUnsupported", version, Version);
        }

        private void HandleXmlBase(XElement xml)
        {
            if (_initialised) return;

            if (xml.Name != "BloxstrapCustomBootstrapper")
                throw new CustomThemeException("CustomTheme.Errors.RootElementInvalid", "BloxstrapCustomBootstrapper");

            AssertThemeVersion(xml.Attribute("Version")?.Value);

            if (xml.Descendants().Count() > MaxElements)
                throw new CustomThemeException("CustomTheme.Errors.ElementLimitReached", MaxElements);

            _initialised = true;
            HandleXmlElement_BloxstrapCustomBootstrapper(this, xml);

            foreach (var child in xml.Elements())
                AddXml(this, child);
        }

        private static void AddXml(CustomDialog dialog, XElement xmlElement)
        {
            if (xmlElement.Name.ToString().Contains('.'))
                return;

            var control = HandleXml<Control>(dialog, xmlElement);

            if (control is not DummyControl)
                dialog.ElementGrid.Children.Add(control);
        }

        public void ApplyCustomTheme(string name)
        {
            string path = Path.Combine(Paths.CustomThemes, name, "Theme.xml");

            if (!File.Exists(path))
                throw new CustomThemeException("CustomTheme.Errors.FileNotFound", path);

            ApplyCustomTheme(name, File.ReadAllText(path));
        }

        public void ApplyCustomTheme(string name, string contents)
        {
            ThemeDir = Path.Combine(Paths.CustomThemes, name);

            string convertedContents = ThemeXamlConverter.ConvertThemeXaml(contents, ThemeDir);
            ThemeFontManager.RegisterThemeFonts(ThemeDir);

            try
            {
                XElement xml = XElement.Parse(convertedContents);
                HandleXmlBase(xml);
            }
            catch (Exception ex) when (ex is not CustomThemeException)
            {
                throw new CustomThemeException(ex, "CustomTheme.Errors.XmlParseFailed");
            }
        }
    }
}