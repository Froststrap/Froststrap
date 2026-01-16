using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Froststrap.UI.Elements.Controls;
using FluentAvalonia.UI.Controls;

namespace Froststrap.UI.Elements.Bootstrapper
{
	public partial class CustomDialog
	{
		#region Transformation
		private static Transform HandleXmlElement_ScaleTransform(CustomDialog dialog, XElement xmlElement)
		{
			return new ScaleTransform
			{
				ScaleX = ParseXmlAttribute<double>(xmlElement, "ScaleX", 1),
				ScaleY = ParseXmlAttribute<double>(xmlElement, "ScaleY", 1),
			};
		}

		private static Transform HandleXmlElement_SkewTransform(CustomDialog dialog, XElement xmlElement)
		{
			return new SkewTransform
			{
				AngleX = ParseXmlAttribute<double>(xmlElement, "AngleX", 0),
				AngleY = ParseXmlAttribute<double>(xmlElement, "AngleY", 0),
			};
		}

		private static Transform HandleXmlElement_RotateTransform(CustomDialog dialog, XElement xmlElement)
		{
			return new RotateTransform
			{
				Angle = ParseXmlAttribute<double>(xmlElement, "Angle", 0),
				CenterX = ParseXmlAttribute<double>(xmlElement, "CenterX", 0),
				CenterY = ParseXmlAttribute<double>(xmlElement, "CenterY", 0)
			};
		}

		private static Transform HandleXmlElement_TranslateTransform(CustomDialog dialog, XElement xmlElement)
		{
			return new TranslateTransform
			{
				X = ParseXmlAttribute<double>(xmlElement, "X", 0),
				Y = ParseXmlAttribute<double>(xmlElement, "Y", 0)
			};
		}
		#endregion

		#region effects
		private static BlurEffect HandleXmlElement_BlurEffect(CustomDialog dialog, XElement xmlElement)
		{
			double radius = ParseXmlAttribute<double>(xmlElement, "Radius", 5);

			var effect = new BlurEffect();
			effect.Radius = ParseXmlAttribute<double>(xmlElement, "Radius", 5);

			return effect;
		}

		private static DropShadowEffect HandleXmlElement_DropShadowEffect(CustomDialog dialog, XElement xmlElement)
		{
			var effect = new DropShadowEffect();
			effect.Color = GetColorFromXElement(xmlElement, "Color") is Color c ? c : Colors.Black;
			effect.BlurRadius = ParseXmlAttribute<double>(xmlElement, "BlurRadius", 5);
			effect.Opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1);

			var color = GetColorFromXElement(xmlElement, "Color");
			if (color is Color)
				effect.Color = (Color)color;

			return effect;
		}

		#endregion

		#region Brushes
		private static void HandleXml_Brush(Brush brush, XElement xmlElement)
		{
			brush.Opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1.0);
		}

		private static Brush HandleXmlElement_SolidColorBrush(CustomDialog dialog, XElement xmlElement)
		{
			var brush = new SolidColorBrush();
			HandleXml_Brush(brush, xmlElement);

			object? color = GetColorFromXElement(xmlElement, "Color");
			if (color is Color c)
				brush.Color = c;

			return brush;
		}

		private static Brush HandleXmlElement_ImageBrush(CustomDialog dialog, XElement xmlElement)
		{
			var imageBrush = new ImageBrush();
			HandleXml_Brush(imageBrush, xmlElement);

			imageBrush.AlignmentX = ParseXmlAttribute<AlignmentX>(xmlElement, "AlignmentX", AlignmentX.Center);
			imageBrush.AlignmentY = ParseXmlAttribute<AlignmentY>(xmlElement, "AlignmentY", AlignmentY.Center);
			imageBrush.Stretch = ParseXmlAttribute<Stretch>(xmlElement, "Stretch", Stretch.Fill);
			imageBrush.TileMode = ParseXmlAttribute<TileMode>(xmlElement, "TileMode", TileMode.None);

			var sourceData = GetImageSourceData(dialog, "ImageSource", xmlElement);

			if (sourceData.IsIcon)
			{
				imageBrush[!ImageBrush.SourceProperty] = new Binding("Icon");
			}
			else
			{
				try
				{
					imageBrush.Source = new Bitmap(sourceData.Uri!.LocalPath);
				}
				catch (Exception ex)
				{
					throw new CustomThemeException(ex, "CustomTheme.Errors.ElementTypeCreationFailed", "Image", "Bitmap", ex.Message);
				}
			}

			return imageBrush;
		}

		private static GradientStop HandleXmlElement_GradientStop(CustomDialog dialog, XElement xmlElement)
		{
			var gs = new GradientStop();
			object? color = GetColorFromXElement(xmlElement, "Color");
			if (color is Color c) gs.Color = c;
			gs.Offset = ParseXmlAttribute<double>(xmlElement, "Offset", 0.0);
			return gs;
		}

		private static Brush HandleXmlElement_LinearGradientBrush(CustomDialog dialog, XElement xmlElement)
		{
			var brush = new LinearGradientBrush();
			HandleXml_Brush(brush, xmlElement);

			if (GetPointFromXElement(xmlElement, "StartPoint") is RelativePoint sp) brush.StartPoint = sp;
			if (GetPointFromXElement(xmlElement, "EndPoint") is RelativePoint ep) brush.EndPoint = ep;

			brush.SpreadMethod = ParseXmlAttribute<GradientSpreadMethod>(xmlElement, "SpreadMethod", GradientSpreadMethod.Pad);

			foreach (var child in xmlElement.Elements())
				brush.GradientStops.Add(HandleXml<GradientStop>(dialog, child));

			return brush;
		}

		private static void ApplyBrush_Control(CustomDialog dialog, Control uiElement, string name, AvaloniaProperty property, XElement xmlElement)
		{
			object? brushAttr = GetBrushFromXElement(xmlElement, name);
			if (brushAttr is Brush b)
			{
				uiElement.SetValue(property, b);
				return;
			}
			else if (brushAttr is string resourceKey)
			{
				uiElement[!property] = uiElement.GetResourceObservable(resourceKey).ToBinding();
				return;
			}

			var brushElement = xmlElement.Element($"{xmlElement.Name}.{name}");
			if (brushElement == null) return;

			if (brushElement.FirstNode is XElement first)
			{
				var brush = HandleXml<Brush>(dialog, first);
				uiElement.SetValue(property, brush);
			}
		}
		#endregion

		#region Shapes
		private static void HandleXmlElement_Shape(CustomDialog dialog, Avalonia.Controls.Shapes.Shape shape, XElement xmlElement)
		{
			HandleXmlElement_Control(dialog, shape, xmlElement);
			ApplyBrush_Control(dialog, shape, "Fill", Avalonia.Controls.Shapes.Shape.FillProperty, xmlElement);
			ApplyBrush_Control(dialog, shape, "Stroke", Avalonia.Controls.Shapes.Shape.StrokeProperty, xmlElement);
			shape.Stretch = ParseXmlAttribute<Stretch>(xmlElement, "Stretch", Stretch.Fill);
			shape.StrokeDashOffset = ParseXmlAttribute<double>(xmlElement, "StrokeDashOffset", 0);
			shape.StrokeJoin = ParseXmlAttribute<PenLineJoin>(xmlElement, "StrokeLineJoin", PenLineJoin.Miter);
			shape.StrokeMiterLimit = ParseXmlAttribute<double>(xmlElement, "StrokeMiterLimit", 10);
			shape.StrokeThickness = ParseXmlAttribute<double>(xmlElement, "StrokeThickness", 1);
		}

		private static Avalonia.Controls.Shapes.Ellipse HandleXmlElement_Ellipse(CustomDialog dialog, XElement xmlElement)
		{
			var ellipse = new Avalonia.Controls.Shapes.Ellipse();
			HandleXmlElement_Shape(dialog, ellipse, xmlElement);
			return ellipse;
		}

		private static Avalonia.Controls.Shapes.Line HandleXmlElement_Line(CustomDialog dialog, XElement xmlElement)
		{
			var line = new Avalonia.Controls.Shapes.Line();
			HandleXmlElement_Shape(dialog, line, xmlElement);
			line.StartPoint = new Point(ParseXmlAttribute<double>(xmlElement, "X1", 0), ParseXmlAttribute<double>(xmlElement, "Y1", 0));
			line.EndPoint = new Point(ParseXmlAttribute<double>(xmlElement, "X2", 0), ParseXmlAttribute<double>(xmlElement, "Y2", 0));
			return line;
		}

		private static Avalonia.Controls.Shapes.Rectangle HandleXmlElement_Rectangle(CustomDialog dialog, XElement xmlElement)
		{
			var rectangle = new Avalonia.Controls.Shapes.Rectangle();
			HandleXmlElement_Shape(dialog, rectangle, xmlElement);
			return rectangle;
		}
		#endregion



		#region Elements
		private static void HandleXmlElement_Control(CustomDialog dialog, Control uiElement, XElement xmlElement)
		{
			string? name = xmlElement.Attribute("Name")?.Value;
			if (name != null)
			{
				if (dialog.UsedNames.Contains(name)) throw new Exception($"{xmlElement.Name} has duplicate name {name}");
				dialog.UsedNames.Add(name);
				uiElement.Name = name;
			}

			uiElement.IsVisible = ParseXmlAttribute<bool>(xmlElement, "IsVisible", true);
			uiElement.IsEnabled = ParseXmlAttribute<bool>(xmlElement, "IsEnabled", true);

			if (GetThicknessFromXElement(xmlElement, "Margin") is Thickness m) uiElement.Margin = m;
			uiElement.Height = ParseXmlAttribute<double>(xmlElement, "Height", double.NaN);
			uiElement.Width = ParseXmlAttribute<double>(xmlElement, "Width", double.NaN);
			uiElement.HorizontalAlignment = ParseXmlAttribute<HorizontalAlignment>(xmlElement, "HorizontalAlignment", HorizontalAlignment.Left);
			uiElement.VerticalAlignment = ParseXmlAttribute<VerticalAlignment>(xmlElement, "VerticalAlignment", VerticalAlignment.Top);
			uiElement.Opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1);

			if (GetPointFromXElement(xmlElement, "RenderTransformOrigin") is RelativePoint rto) uiElement.RenderTransformOrigin = rto;

			uiElement.ZIndex = ParseXmlAttributeClamped(xmlElement, "Panel.ZIndex", 0, 0, 1000);

			Grid.SetRow(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.Row", 0));
			Grid.SetRowSpan(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.RowSpan", 1));
			Grid.SetColumn(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.Column", 0));
			Grid.SetColumnSpan(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.ColumnSpan", 1));

			if (uiElement is TemplatedControl tc)
			{
				if (GetThicknessFromXElement(xmlElement, "Padding") is Thickness p) tc.Padding = p;
				if (GetThicknessFromXElement(xmlElement, "BorderThickness") is Thickness bt) tc.BorderThickness = bt;
				ApplyBrush_Control(dialog, tc, "Foreground", TemplatedControl.ForegroundProperty, xmlElement);
				ApplyBrush_Control(dialog, tc, "Background", TemplatedControl.BackgroundProperty, xmlElement);
				ApplyBrush_Control(dialog, tc, "BorderBrush", TemplatedControl.BorderBrushProperty, xmlElement);

				if (ParseXmlAttributeNullable<double>(xmlElement, "FontSize") is double fs) tc.FontSize = fs;
				tc.FontWeight = GetFontWeightFromXElement(xmlElement);
				tc.FontStyle = GetFontStyleFromXElement(xmlElement);
			}

			ApplyTransformations_Control(dialog, uiElement, xmlElement);
		}

		private static Control HandleXmlElement_FroststrapCustomBootstrapper(CustomDialog dialog, XElement xmlElement)
		{
			xmlElement.SetAttributeValue("IsVisible", "False");
			xmlElement.SetAttributeValue("IsEnabled", "True");
			HandleXmlElement_Control(dialog, dialog, xmlElement);

			dialog.Opacity = 1;
			dialog.ElementGrid.RenderTransform = dialog.RenderTransform;
			dialog.RenderTransform = null;

			var bootstrapperTheme = ParseXmlAttribute<Theme>(xmlElement, "Theme", Enums.Theme.Default);

			if (bootstrapperTheme == Enums.Theme.Default)
				bootstrapperTheme = App.Settings.Prop.Theme;

			dialog.RequestedThemeVariant = bootstrapperTheme.GetFinal() == Enums.Theme.Light
				? ThemeVariant.Light
				: ThemeVariant.Dark;

			dialog.ElementGrid.Margin = dialog.Margin;
			dialog.Margin = new Thickness(0);
			dialog.Padding = new Thickness(0);

			string title = xmlElement.Attribute("Title")?.Value ?? "Froststrap";
			dialog.Title = title;

			if (ParseXmlAttribute<bool>(xmlElement, "IgnoreTitleBarInset", false))
			{
				Grid.SetRow(dialog.ElementGrid, 0);
				Grid.SetRowSpan(dialog.ElementGrid, 2);
			}

			return new DummyControl();
		}

		private static Control HandleXmlElement_FroststrapCustomBootstrapper_Fake(CustomDialog dialog, XElement xmlElement)
		{
			// this only exists to error out the theme if someone tries to use two BloxstrapCustomBootstrappers
			throw new CustomThemeException("CustomTheme.Errors.ElementInvalidChild", xmlElement.Parent!.Name.ToString(), xmlElement.Name.ToString());
		}

		private static Control HandleXmlElement_TitleBar(CustomDialog dialog, XElement xmlElement)
		{
			xmlElement.SetAttributeValue("Name", "TitleBar");
			xmlElement.SetAttributeValue("IsEnabled", "True");

			HandleXmlElement_Control(dialog, dialog.RootTitleBar, xmlElement);

			dialog.RootTitleBar.RenderTransform = null;


			dialog.RootTitleBar.ZIndex = 1001;
			dialog.RootTitleBar.Height = double.NaN;
			dialog.RootTitleBar.Width = double.NaN;
			dialog.RootTitleBar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
			dialog.RootTitleBar.Margin = new Thickness(0);

			if (dialog is Window window)
			{
				window.CanMinimize = ParseXmlAttribute<bool>(xmlElement, "ShowMinimize", true);
				bool showClose = ParseXmlAttribute<bool>(xmlElement, "ShowClose", true);
				window.Title = xmlElement.Attribute("Title")?.Value ?? "Bloxstrap";


				window.Closing += (_, e) =>
				{
					if (!showClose)
						e.Cancel = true;
				};
			}

			return new DummyControl();
		}


		private static Control HandleXmlElement_Button(CustomDialog dialog, XElement xmlElement)
		{
			var button = new Button();
			HandleXmlElement_Control(dialog, button, xmlElement);
			button.Content = GetContentFromXElement(dialog, xmlElement);

			if (xmlElement.Attribute("Name")?.Value == "CancelButton")
			{
				button[!Button.IsEnabledProperty] = new Binding("CancelEnabled");
				button[!Button.CommandProperty] = new Binding("CancelInstallCommand");
			}

			return button;
		}

		private static void HandleXmlElement_RangeBase(CustomDialog dialog, RangeBase rangeBase, XElement xmlElement)
		{
			HandleXmlElement_Control(dialog, rangeBase, xmlElement);

			rangeBase.Value = ParseXmlAttribute<double>(xmlElement, "Value", 0);
			rangeBase.Maximum = ParseXmlAttribute<double>(xmlElement, "Maximum", 100);
		}

		private static Control HandleXmlElement_ProgressBar(CustomDialog dialog, XElement xmlElement)
		{
			var progressBar = new ProgressBar();
			HandleXmlElement_Control(dialog, progressBar, xmlElement);
			progressBar.IsIndeterminate = ParseXmlAttribute<bool>(xmlElement, "IsIndeterminate", false);
			progressBar.Value = ParseXmlAttribute<double>(xmlElement, "Value", 0);
			progressBar.Maximum = ParseXmlAttribute<double>(xmlElement, "Maximum", 100);

			if (xmlElement.Attribute("Name")?.Value == "PrimaryProgressBar")
			{
				progressBar[!ProgressBar.IsIndeterminateProperty] = new Binding("ProgressIndeterminate");
				progressBar[!ProgressBar.MaximumProperty] = new Binding("ProgressMaximum");
				progressBar[!ProgressBar.ValueProperty] = new Binding("ProgressValue");
			}

			return progressBar;
		}

		private static Control HandleXmlElement_ProgressRing(CustomDialog dialog, XElement xmlElement)
		{
			var progressRing = new ProgressRing();

			HandleXmlElement_RangeBase(dialog, progressRing, xmlElement);

			progressRing.IsIndeterminate = ParseXmlAttribute<bool>(xmlElement, "IsIndeterminate", false);

			if (xmlElement.Attribute("Name")?.Value == "PrimaryProgressRing")
			{
				progressRing.Bind(ProgressRing.IsIndeterminateProperty, new Binding("ProgressIndeterminate")
				{
					Mode = BindingMode.OneWay
				});

				progressRing.Bind(ProgressRing.MaximumProperty, new Binding("ProgressMaximum")
				{
					Mode = BindingMode.OneWay
				});

				progressRing.Bind(ProgressRing.ValueProperty, new Binding("ProgressValue")
				{
					Mode = BindingMode.OneWay
				});
			}

			return progressRing;
		}

		private static void HandleXmlElement_TextBlock_Base(CustomDialog dialog, TextBlock textBlock, XElement xmlElement)
		{
			HandleXmlElement_Control(dialog, textBlock, xmlElement);

			ApplyBrush_Control(dialog, textBlock, "Foreground", TextBlock.ForegroundProperty, xmlElement);
			ApplyBrush_Control(dialog, textBlock, "Background", TextBlock.BackgroundProperty, xmlElement);

			var fontSize = ParseXmlAttributeNullable<double>(xmlElement, "FontSize");
			if (fontSize.HasValue)
				textBlock.FontSize = fontSize.Value;

			textBlock.FontWeight = GetFontWeightFromXElement(xmlElement);
			textBlock.FontStyle = GetFontStyleFromXElement(xmlElement);

			textBlock.LineHeight = ParseXmlAttribute<double>(xmlElement, "LineHeight", double.NaN);

			textBlock.TextAlignment = ParseXmlAttribute<TextAlignment>(xmlElement, "TextAlignment", TextAlignment.Center);
			//textBlock.TextTrimming = ParseXmlAttribute<TextTrimming>(xmlElement, "TextTrimming", TextTrimming.None);
			textBlock.TextWrapping = ParseXmlAttribute<TextWrapping>(xmlElement, "TextWrapping", TextWrapping.NoWrap);
			textBlock.TextDecorations = GetTextDecorationsFromXElement(xmlElement);

			string? fontFamily = GetFullPath(dialog, xmlElement.Attribute("FontFamily")?.Value);
			if (!string.IsNullOrEmpty(fontFamily))
				textBlock.FontFamily = new Avalonia.Media.FontFamily(fontFamily);

			object? padding = GetThicknessFromXElement(xmlElement, "Padding");
			if (padding is Thickness thickness)
				textBlock.Padding = thickness;
		}

		private static Control HandleXmlElement_TextBlock(CustomDialog dialog, XElement xmlElement)
		{
			var textBlock = new TextBlock();
			HandleXmlElement_Control(dialog, textBlock, xmlElement);

			textBlock.Text = GetTranslatedText(xmlElement.Attribute("Text")?.Value);
			textBlock.TextAlignment = ParseXmlAttribute<TextAlignment>(xmlElement, "TextAlignment", TextAlignment.Center);
			textBlock.TextWrapping = ParseXmlAttribute<TextWrapping>(xmlElement, "TextWrapping", TextWrapping.NoWrap);

			if (ParseXmlAttributeNullable<double>(xmlElement, "FontSize") is double fs) textBlock.FontSize = fs;
			textBlock.FontWeight = GetFontWeightFromXElement(xmlElement);
			textBlock.FontStyle = GetFontStyleFromXElement(xmlElement);
			ApplyBrush_Control(dialog, textBlock, "Foreground", TextBlock.ForegroundProperty, xmlElement);

			if (xmlElement.Attribute("Name")?.Value == "StatusText")
				textBlock[!TextBlock.TextProperty] = new Binding("Message");

			return textBlock;
		}

		private static Control HandleXmlElement_MarkdownTextBlock(CustomDialog dialog, XElement xmlElement)
		{
			var textBlock = new MarkdownTextBlock();
			HandleXmlElement_TextBlock_Base(dialog, textBlock, xmlElement);

			string? text = GetTranslatedText(xmlElement.Attribute("Text")?.Value);
			if (text != null)
				textBlock.MarkdownText = text;

			return textBlock;
		}

		private static Control HandleXmlElement_Image(CustomDialog dialog, XElement xmlElement)
		{
			var image = new Image();
			HandleXmlElement_Control(dialog, image, xmlElement);
			image.Stretch = ParseXmlAttribute<Stretch>(xmlElement, "Stretch", Stretch.Uniform);

			var sourceData = GetImageSourceData(dialog, "Source", xmlElement);
			if (sourceData.IsIcon)
			{
				image[!Image.SourceProperty] = new Binding("Icon");
			}
			else if (sourceData.Uri != null)
			{
				try
				{
					image.Source = new Bitmap(sourceData.Uri.LocalPath);
				}
				catch (Exception ex)
				{
					throw new CustomThemeException(ex, "CustomTheme.Errors.ElementTypeCreationFailed", "Image", "Bitmap", ex.Message);
				}
			}

			return image;
		}

		private static RowDefinition HandleXmlElement_RowDefinition(CustomDialog dialog, XElement xmlElement)
		{
			var rowDefinition = new RowDefinition();

			var height = GetGridLengthFromXElement(xmlElement, "Height");
			if (height != null)
				rowDefinition.Height = (GridLength)height;

			rowDefinition.MinHeight = ParseXmlAttribute<double>(xmlElement, "MinHeight", 0);
			rowDefinition.MaxHeight = ParseXmlAttribute<double>(xmlElement, "MaxHeight", double.PositiveInfinity);

			return rowDefinition;
		}

		private static ColumnDefinition HandleXmlElement_ColumnDefinition(CustomDialog dialog, XElement xmlElement)
		{
			var columnDefinition = new ColumnDefinition();

			var width = GetGridLengthFromXElement(xmlElement, "Width");
			if (width != null)
				columnDefinition.Width = (GridLength)width;

			columnDefinition.MinWidth = ParseXmlAttribute<double>(xmlElement, "MinWidth", 0);
			columnDefinition.MaxWidth = ParseXmlAttribute<double>(xmlElement, "MaxWidth", double.PositiveInfinity);

			return columnDefinition;
		}

		private static void HandleXmlElement_Grid_RowDefinitions(Grid grid, CustomDialog dialog, XElement xmlElement)
		{
			foreach (var element in xmlElement.Elements())
			{
				var rowDefinition = HandleXml<RowDefinition>(dialog, element);
				grid.RowDefinitions.Add(rowDefinition);
			}
		}

		private static void HandleXmlElement_Grid_ColumnDefinitions(Grid grid, CustomDialog dialog, XElement xmlElement)
		{
			foreach (var element in xmlElement.Elements())
			{
				var columnDefinition = HandleXml<ColumnDefinition>(dialog, element);
				grid.ColumnDefinitions.Add(columnDefinition);
			}
		}

		private static Grid HandleXmlElement_Grid(CustomDialog dialog, XElement xmlElement)
		{
			var grid = new Grid();
			HandleXmlElement_Control(dialog, grid, xmlElement);

			foreach (var element in xmlElement.Elements())
			{
				if (element.Name == "Grid.RowDefinitions")
				{
					foreach (var r in element.Elements())
						grid.RowDefinitions.Add(new RowDefinition(GridLength.Parse(r.Attribute("Height")?.Value ?? "Auto")));
				}
				else if (element.Name == "Grid.ColumnDefinitions")
				{
					foreach (var c in element.Elements())
						grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Parse(c.Attribute("Width")?.Value ?? "Auto")));
				}
				else if (!element.Name.ToString().Contains("Grid."))
				{
					grid.Children.Add(HandleXml<Control>(dialog, element));
				}
			}
			return grid;
		}

		private static StackPanel HandleXmlElement_StackPanel(CustomDialog dialog, XElement xmlElement)
		{
			var stackPanel = new StackPanel();
			HandleXmlElement_Control(dialog, stackPanel, xmlElement);

			stackPanel.Orientation = ParseXmlAttribute<Orientation>(xmlElement, "Orientation", Orientation.Vertical);

			foreach (var element in xmlElement.Elements())
			{
				var uiElement = HandleXml<Control>(dialog, element);
				stackPanel.Children.Add(uiElement);
			}

			return stackPanel;
		}

		private static Border HandleXmlElement_Border(CustomDialog dialog, XElement xmlElement)
		{
			var border = new Border();
			HandleXmlElement_Control(dialog, border, xmlElement);
			ApplyBrush_Control(dialog, border, "Background", Border.BackgroundProperty, xmlElement);
			ApplyBrush_Control(dialog, border, "BorderBrush", Border.BorderBrushProperty, xmlElement);
			if (GetCornerRadiusFromXElement(xmlElement, "CornerRadius") is CornerRadius cr) border.CornerRadius = cr;

			var child = xmlElement.Elements().FirstOrDefault(x => !x.Name.ToString().Contains("Border."));
			if (child != null) border.Child = HandleXml<Control>(dialog, child);

			return border;
		}
		#endregion
	}
}