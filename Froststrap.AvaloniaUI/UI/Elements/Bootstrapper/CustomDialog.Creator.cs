using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Froststrap;

namespace Froststrap.UI.Elements.Bootstrapper
{
	public partial class CustomDialog
	{
		const int Version = 1;

		private class DummyControl : Control { }

		private const int MaxElements = 100;

		private bool _initialised = false;

		private List<string> UsedNames { get; } = new List<string>();

		private string ThemeDir { get; set; } = "";

		delegate object HandleXmlElementDelegate(CustomDialog dialog, XElement xmlElement);

		private static Dictionary<string, HandleXmlElementDelegate> _elementHandlerMap = new Dictionary<string, HandleXmlElementDelegate>()
		{
			["BloxstrapCustomBootstrapper"] = HandleXmlElement_FroststrapCustomBootstrapper_Fake,
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

			["BlurEffect"] = HandleXmlElement_BlurEffect,
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
				throw new CustomThemeException("CustomTheme.Errors.UnknownElement", xmlElement.Name);

			var element = _elementHandlerMap[xmlElement.Name.ToString()](dialog, xmlElement);
			if (element is not T)
				throw new CustomThemeException("CustomTheme.Errors.ElementInvalidChild", xmlElement.Parent!.Name, xmlElement.Name);

			return (T)element;
		}

		private static void AddXml(CustomDialog dialog, XElement xmlElement)
		{
			if (xmlElement.Name.ToString().StartsWith($"{xmlElement.Parent!.Name}."))
				return;

			var control = HandleXml<Control>(dialog, xmlElement);
			if (control is not DummyControl)
				dialog.ElementGrid.Children.Add(control);
		}

		private static void AssertThemeVersion(string? versionStr)
		{
			if (string.IsNullOrEmpty(versionStr))
				throw new CustomThemeException("CustomTheme.Errors.VersionNotSet", "BloxstrapCustomBootstrapper");

			if (!uint.TryParse(versionStr, out uint version))
				throw new CustomThemeException("CustomTheme.Errors.VersionNotNumber", "BloxstrapCustomBootstrapper");

			switch (version)
			{
				case Version:
					break;
				case 0:
					throw new CustomThemeException("CustomTheme.Errors.VersionNotSupported", "BloxstrapCustomBootstrapper", version);
				default:
					throw new CustomThemeException("CustomTheme.Errors.VersionNotRecognised", "BloxstrapCustomBootstrapper", version);
			}
		}

		private void HandleXmlBase(XElement xml)
		{
			if (_initialised)
				throw new CustomThemeException("CustomTheme.Errors.DialogAlreadyInitialised");

			if (xml.Name != "BloxstrapCustomBootstrapper")
				throw new CustomThemeException("CustomTheme.Errors.InvalidRoot", "BloxstrapCustomBootstrapper");

			AssertThemeVersion(xml.Attribute("Version")?.Value);

			if (xml.Descendants().Count() > MaxElements)
				throw new CustomThemeException("CustomTheme.Errors.TooManyElements", MaxElements, xml.Descendants().Count());

			_initialised = true;

			HandleXmlElement_FroststrapCustomBootstrapper(this, xml);

			foreach (var child in xml.Elements())
				AddXml(this, child);
		}

		#region Public APIs
		public void ApplyCustomTheme(string name, string contents)
		{
			ThemeDir = System.IO.Path.Combine(Paths.CustomThemes, name);

			XElement xml;

			try
			{
				using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(contents)))
					xml = XElement.Load(ms);
			}
			catch (Exception ex)
			{
				throw new CustomThemeException(ex, "CustomTheme.Errors.XMLParseFailed", ex.Message);
			}

			HandleXmlBase(xml);
		}

		public void ApplyCustomTheme(string name)
		{
			string path = System.IO.Path.Combine(Paths.CustomThemes, name, "Theme.xml");

			ApplyCustomTheme(name, File.ReadAllText(path));
		}
		#endregion
	}
}