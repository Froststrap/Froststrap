using AnimatedImage.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Froststrap.UI.Elements.Controls;
using Froststrap.UI.Utility;
using SukiUI.Controls;
using System.Xml.Linq;

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
                ScaleY = ParseXmlAttribute<double>(xmlElement, "ScaleY", 1)
            };
        }

        private static Transform HandleXmlElement_SkewTransform(CustomDialog dialog, XElement xmlElement)
        {
            return new SkewTransform
            {
                AngleX = ParseXmlAttribute<double>(xmlElement, "AngleX", 0),
                AngleY = ParseXmlAttribute<double>(xmlElement, "AngleY", 0)
            };
        }

        private static Transform HandleXmlElement_RotateTransform(CustomDialog dialog, XElement xmlElement)
        {
            var rt = new RotateTransform();

            rt.Angle = ParseXmlAttribute<double>(xmlElement, "Angle", 0);
            rt.CenterX = ParseXmlAttribute<double>(xmlElement, "CenterX", 0);
            rt.CenterY = ParseXmlAttribute<double>(xmlElement, "CenterY", 0);

            return rt;
        }

        private static Transform HandleXmlElement_TranslateTransform(CustomDialog dialog, XElement xmlElement)
        {
            var tt = new TranslateTransform();

            tt.X = ParseXmlAttribute<double>(xmlElement, "X", 0);
            tt.Y = ParseXmlAttribute<double>(xmlElement, "Y", 0);

            return tt;
        }
        #endregion

        #region Effects
        private static object HandleXmlElement_DropShadowEffect(CustomDialog dialog, XElement xmlElement)
        {
            var color = ParseXmlAttribute<Color>(xmlElement, "Color", Colors.Black);
            double opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1.0);
            double blur = ParseXmlAttribute<double>(xmlElement, "BlurRadius", 5.0);
            double offsetX = ParseXmlAttribute<double>(xmlElement, "OffsetX", 0.0);
            double offsetY = ParseXmlAttribute<double>(xmlElement, "OffsetY", 0.0);

            return new BoxShadows(new BoxShadow
            {
                Color = new Color((byte)(opacity * 255), color.R, color.G, color.B),
                Blur = blur,
                OffsetX = offsetX,
                OffsetY = offsetY,
                Spread = 0
            });
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
            if (color is Color)
                brush.Color = (Color)color;

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

            var viewbox = GetRectFromXElement(xmlElement, "Viewbox");
            if (viewbox is Rect vb)
                imageBrush.SourceRect = new RelativeRect(vb, RelativeUnit.Relative);

            var viewport = GetRectFromXElement(xmlElement, "Viewport");
            if (viewport is Rect vp)
                imageBrush.DestinationRect = new RelativeRect(vp, RelativeUnit.Relative);

            var sourceData = GetImageSourceData(dialog, "ImageSource", xmlElement);

            if (sourceData.IsIcon)
            {
                imageBrush.Bind(ImageBrush.SourceProperty, new Binding("Icon"));
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
            if (color is Color)
                gs.Color = (Color)color;

            gs.Offset = ParseXmlAttribute<double>(xmlElement, "Offset", 0.0);

            return gs;
        }

        private static Brush HandleXmlElement_LinearGradientBrush(CustomDialog dialog, XElement xmlElement)
        {
            var brush = new LinearGradientBrush();
            HandleXml_Brush(brush, xmlElement);

            var startPoint = GetPointFromXElement(xmlElement, "StartPoint");
            if (startPoint is Point sp)
                brush.StartPoint = new RelativePoint(sp, RelativeUnit.Relative);

            var endPoint = GetPointFromXElement(xmlElement, "EndPoint");
            if (endPoint is Point ep)
                brush.EndPoint = new RelativePoint(ep, RelativeUnit.Relative);

            brush.SpreadMethod = ParseXmlAttribute<GradientSpreadMethod>(xmlElement, "SpreadMethod", GradientSpreadMethod.Pad);

            foreach (var child in xmlElement.Elements())
            {
                var stop = HandleXml<GradientStop>(dialog, child);
                if (stop != null)
                    brush.GradientStops.Add(stop);
            }

            return brush;
        }

        private static void ApplyBrush_Control(CustomDialog dialog, Control uiElement, string name, AvaloniaProperty property, XElement xmlElement)
        {
            object? brushAttr = GetBrushFromXElement(xmlElement, name);

            if (brushAttr is Brush brush)
            {
                uiElement.SetValue(property, brush);
                return;
            }
            else if (brushAttr is string resourceKey)
            {
                uiElement.Bind(property, new DynamicResourceExtension(resourceKey));
                return;
            }

            var brushElement = xmlElement.Element($"{xmlElement.Name}.{name}");
            if (brushElement == null)
                return;

            var first = brushElement.FirstNode as XElement;
            if (first == null)
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissingChild", xmlElement.Name.ToString(), name);

            var customBrush = HandleXml<Brush>(dialog, first);
            uiElement.SetValue(property, customBrush);
        }
        #endregion

        #region Shapes
        private static void HandleXmlElement_Shape(CustomDialog dialog, Shape shape, XElement xmlElement)
        {
            HandleXmlElement_Control(dialog, shape, xmlElement);

            ApplyBrush_Control(dialog, shape, "Fill", Shape.FillProperty, xmlElement);
            ApplyBrush_Control(dialog, shape, "Stroke", Shape.StrokeProperty, xmlElement);

            shape.Stretch = ParseXmlAttribute<Stretch>(xmlElement, "Stretch", Stretch.Fill);
            shape.StrokeDashOffset = ParseXmlAttribute<double>(xmlElement, "StrokeDashOffset", 0);

            shape.StrokeJoin = ParseXmlAttribute<PenLineJoin>(xmlElement, "StrokeJoin", PenLineJoin.Miter);
            shape.StrokeMiterLimit = ParseXmlAttribute<double>(xmlElement, "StrokeMiterLimit", 10);
            shape.StrokeLineCap = ParseXmlAttribute<PenLineCap>(xmlElement, "StrokeLineCap", PenLineCap.Flat);
            shape.StrokeThickness = ParseXmlAttribute<double>(xmlElement, "StrokeThickness", 1);
        }

        private static Ellipse HandleXmlElement_Ellipse(CustomDialog dialog, XElement xmlElement)
        {
            var ellipse = new Ellipse();
            HandleXmlElement_Shape(dialog, ellipse, xmlElement);

            return ellipse;
        }

        private static Line HandleXmlElement_Line(CustomDialog dialog, XElement xmlElement)
        {
            var line = new Line();
            HandleXmlElement_Shape(dialog, line, xmlElement);

            line.StartPoint = new Point(ParseXmlAttribute<double>(xmlElement, "X1", 0), ParseXmlAttribute<double>(xmlElement, "Y1", 0));
            line.EndPoint = new Point(ParseXmlAttribute<double>(xmlElement, "X2", 0), ParseXmlAttribute<double>(xmlElement, "Y2", 0));

            return line;
        }

        private static Rectangle HandleXmlElement_Rectangle(CustomDialog dialog, XElement xmlElement)
        {
            var rectangle = new Rectangle();
            HandleXmlElement_Shape(dialog, rectangle, xmlElement);

            rectangle.RadiusX = ParseXmlAttribute<double>(xmlElement, "RadiusX", 0);
            rectangle.RadiusY = ParseXmlAttribute<double>(xmlElement, "RadiusY", 0);

            return rectangle;
        }

        #endregion

        #region Elements
        private static void HandleXmlElement_FrameworkElement(CustomDialog dialog, Control uiElement, XElement xmlElement)
        {
            string? name = xmlElement.Attribute("Name")?.Value;

            if (!string.IsNullOrEmpty(name) && uiElement.Name != name)
            {
                try
                {
                    uiElement.Name = name;
                }
                catch (InvalidOperationException)
                {
                }
            }

            string? visibility = xmlElement.Attribute("Visibility")?.Value;

            if (!string.IsNullOrEmpty(visibility))
            {
                if (visibility.Equals("Collapsed", StringComparison.OrdinalIgnoreCase) ||
                    visibility.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                {
                    uiElement.IsVisible = false;
                }
                else
                {
                    uiElement.IsVisible = true;
                }
            }
            else
            {
                uiElement.IsVisible = ParseXmlAttribute<bool>(xmlElement, "IsVisible", true);
            }

            uiElement.IsEnabled = ParseXmlAttribute<bool>(xmlElement, "IsEnabled", true);

            var margin = GetThicknessFromXElement(xmlElement, "Margin");
            if (margin is Thickness thickness)
                uiElement.Margin = thickness;

            uiElement.Height = ParseXmlAttribute<double>(xmlElement, "Height", double.NaN);
            uiElement.Width = ParseXmlAttribute<double>(xmlElement, "Width", double.NaN);

            uiElement.HorizontalAlignment = ParseXmlAttribute<HorizontalAlignment>(xmlElement, "HorizontalAlignment", HorizontalAlignment.Left);
            uiElement.VerticalAlignment = ParseXmlAttribute<VerticalAlignment>(xmlElement, "VerticalAlignment", VerticalAlignment.Top);

            uiElement.Opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1);

            ApplyBrush_Control(dialog, uiElement, "OpacityMask", Visual.OpacityMaskProperty, xmlElement);

            var renderTransformOrigin = GetPointFromXElement(xmlElement, "RenderTransformOrigin");
            if (renderTransformOrigin is Point point)
            {
                uiElement.RenderTransformOrigin = new RelativePoint(point, RelativeUnit.Relative);
            }

            uiElement.ZIndex = ParseXmlAttributeClamped(xmlElement, "Panel.ZIndex", defaultValue: 0, min: 0, max: 1000);

            Grid.SetRow(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.Row", 0));
            Grid.SetRowSpan(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.RowSpan", 1));
            Grid.SetColumn(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.Column", 0));
            Grid.SetColumnSpan(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.ColumnSpan", 1));

            ApplyTransformations_Control(dialog, uiElement, xmlElement);
            ApplyEffects_Control(dialog, uiElement, xmlElement);
        }

        private static void HandleXmlElement_Control(CustomDialog dialog, Control uiElement, XElement xmlElement)
        {
            HandleXmlElement_FrameworkElement(dialog, uiElement, xmlElement);

            if (uiElement is TemplatedControl templated)
            {
                var padding = GetThicknessFromXElement(xmlElement, "Padding");
                if (padding is Thickness p)
                    templated.Padding = p;

                var borderThickness = GetThicknessFromXElement(xmlElement, "BorderThickness");
                if (borderThickness is Thickness bt)
                    templated.BorderThickness = bt;

                ApplyBrush_Control(dialog, templated, "Foreground", TemplatedControl.ForegroundProperty, xmlElement);
                ApplyBrush_Control(dialog, templated, "Background", TemplatedControl.BackgroundProperty, xmlElement);
                ApplyBrush_Control(dialog, templated, "BorderBrush", TemplatedControl.BorderBrushProperty, xmlElement);

                var fontSize = ParseXmlAttributeNullable<double>(xmlElement, "FontSize");
                if (fontSize is double fs)
                    templated.FontSize = fs;

                templated.FontWeight = GetFontWeightFromXElement(xmlElement);
                templated.FontStyle = GetFontStyleFromXElement(xmlElement);

                string? fontFamilyAttr = xmlElement.Attribute("FontFamily")?.Value;
                if (!string.IsNullOrEmpty(fontFamilyAttr))
                {
                    string resolvedUri = ThemeFontManager.ResolveFontUri(fontFamilyAttr, dialog.ThemeDir);
                    templated.FontFamily = new Avalonia.Media.FontFamily(resolvedUri);
                }
            }
        }

        private static Control HandleXmlElement_BloxstrapCustomBootstrapper(CustomDialog dialog, XElement xmlElement)
        {
            xmlElement.SetAttributeValue("IsVisible", "False");
            xmlElement.SetAttributeValue("IsEnabled", "True");
            HandleXmlElement_Control(dialog, dialog, xmlElement);

            dialog.Opacity = 1;

            dialog.ElementGrid.RenderTransform = dialog.RenderTransform;
            dialog.RenderTransform = null;

            var theme = ParseXmlAttribute<Theme>(xmlElement, "Theme", Enums.Theme.Default);
            if (theme == Enums.Theme.Default)
                theme = App.Settings.Prop.Theme;

            var finalTheme = theme.GetFinal();
            dialog.RequestedThemeVariant = finalTheme == Enums.Theme.Light ? ThemeVariant.Light : ThemeVariant.Dark;

            dialog.ExtendClientAreaToDecorationsHint = ParseXmlAttribute<bool>(xmlElement, "ExtendClientAreaToDecorationsHint", true);

            dialog.ElementGrid.Margin = dialog.Margin;

            dialog.Margin = new Thickness(0);
            dialog.Padding = new Thickness(0);

            string title = xmlElement.Attribute("Title")?.Value ?? "Froststrap";
            dialog.Title = title;

            bool ignoreTitleBarInset = ParseXmlAttribute<bool>(xmlElement, "IgnoreTitleBarInset", false);
            if (ignoreTitleBarInset)
            {
                Grid.SetRow(dialog.ElementGrid, 0);
                Grid.SetRowSpan(dialog.ElementGrid, 2);
            }

            return new DummyControl();
        }

        private static Control HandleXmlElement_BloxstrapCustomBootstrapper_Fake(CustomDialog dialog, XElement xmlElement)
        {
            throw new CustomThemeException("CustomTheme.Errors.ElementInvalidChild", xmlElement.Parent!.Name.ToString(), xmlElement.Name.ToString());
        }

        private static Control HandleXmlElement_TitleBar(CustomDialog dialog, XElement xmlElement)
        {
            var titleBar = new TitleBar();

            HandleXmlElement_Control(dialog, titleBar, xmlElement);

            titleBar.HorizontalAlignment = HorizontalAlignment.Stretch;
            titleBar.Width = double.NaN;

            bool isHidden = ParseXmlAttribute(xmlElement, "IsHidden", false);

            if (isHidden)
            {
                titleBar.IsVisible = false;
                titleBar.Height = 0;
            }
            else
            {
                if (double.IsNaN(titleBar.Height))
                    titleBar.Height = 32;
            }

            titleBar.ZIndex = ParseXmlAttribute<int>(xmlElement, "Panel.ZIndex", 1001);
            titleBar.ShowMinimize = ParseXmlAttribute<bool>(xmlElement, "ShowMinimize", true);
            titleBar.ShowClose = ParseXmlAttribute<bool>(xmlElement, "ShowClose", true);

            string title = xmlElement.Attribute("Title")?.Value ?? "Froststrap";
            titleBar.Title = title;

            return titleBar;
        }

        private static Control HandleXmlElement_Button(CustomDialog dialog, XElement xmlElement)
        {
            var button = new Button();
            HandleXmlElement_Control(dialog, button, xmlElement);

            button.Content = GetContentFromXElement(dialog, xmlElement);

            if (xmlElement.Attribute("Name")?.Value == "CancelButton")
            {
                button.Bind(Control.IsEnabledProperty, new Binding("CancelEnabled"));
                button.Bind(Button.CommandProperty, new Binding("CancelInstallCommand"));
            }

            return button;
        }

        private static void HandleXmlElement_RangeBase(CustomDialog dialog, RangeBase rangeBase, XElement xmlElement)
        {
            HandleXmlElement_Control(dialog, rangeBase, xmlElement);

            ApplyBrush_Control(dialog, rangeBase, "Foreground", TemplatedControl.ForegroundProperty, xmlElement);

            rangeBase.Value = ParseXmlAttribute<double>(xmlElement, "Value", 0);
            rangeBase.Maximum = ParseXmlAttribute<double>(xmlElement, "Maximum", 100);
        }

        private static Control HandleXmlElement_ProgressBar(CustomDialog dialog, XElement xmlElement)
        {
            var progressBar = new ProgressBar();
            HandleXmlElement_RangeBase(dialog, progressBar, xmlElement);

            var fgColorAttr = xmlElement.Attribute("Foreground")?.Value;
            if (!string.IsNullOrEmpty(fgColorAttr) && Color.TryParse(fgColorAttr, out var parsedColor))
            {
                progressBar.Foreground = new SolidColorBrush(parsedColor);
            }

            progressBar.IsIndeterminate = ParseXmlAttribute<bool>(xmlElement, "IsIndeterminate", false);

            if (xmlElement.Attribute("Name")?.Value == "PrimaryProgressBar")
            {
                progressBar.Bind(ProgressBar.IsIndeterminateProperty, new Binding("ProgressIndeterminate"));
                progressBar.Bind(RangeBase.ValueProperty, new Binding("ProgressValue"));
                progressBar.Bind(RangeBase.MaximumProperty, new Binding("ProgressMaximum"));
            }

            return progressBar;
        }

        private static Control HandleXmlElement_ProgressRing(CustomDialog dialog, XElement xmlElement)
        {
            var progressRing = new ProgressBar();

            progressRing.Classes.Add("Ring");

            HandleXmlElement_RangeBase(dialog, progressRing, xmlElement);

            var fgColorAttr = xmlElement.Attribute("Foreground")?.Value;
            if (!string.IsNullOrEmpty(fgColorAttr) && Color.TryParse(fgColorAttr, out var parsedColor))
            {
                progressRing.Foreground = new SolidColorBrush(parsedColor);
            }

            progressRing.IsIndeterminate = ParseXmlAttribute<bool>(xmlElement, "IsIndeterminate", false);

            if (xmlElement.Attribute("Name")?.Value == "PrimaryProgressRing")
            {
                progressRing.Bind(ProgressBar.IsIndeterminateProperty, new Binding("ProgressIndeterminate"));
                progressRing.Bind(RangeBase.ValueProperty, new Binding("ProgressValue"));
                progressRing.Bind(RangeBase.MaximumProperty, new Binding("ProgressMaximum"));
            }

            return progressRing;
        }

        private static void HandleXmlElement_TextBlock_Base(CustomDialog dialog, TextBlock textBlock, XElement xmlElement)
        {
            HandleXmlElement_FrameworkElement(dialog, textBlock, xmlElement);

            ApplyBrush_Control(dialog, textBlock, "Foreground", TextBlock.ForegroundProperty, xmlElement);
            ApplyBrush_Control(dialog, textBlock, "Background", TextBlock.BackgroundProperty, xmlElement);

            var fontSize = ParseXmlAttributeNullable<double>(xmlElement, "FontSize");
            if (fontSize is double fs)
                textBlock.FontSize = fs;

            textBlock.FontWeight = GetFontWeightFromXElement(xmlElement);
            textBlock.FontStyle = GetFontStyleFromXElement(xmlElement);

            textBlock.LineHeight = ParseXmlAttribute<double>(xmlElement, "LineHeight", double.NaN);

            textBlock.TextAlignment = ParseXmlAttribute<TextAlignment>(xmlElement, "TextAlignment", TextAlignment.Center);
            textBlock.TextTrimming = ParseXmlAttribute<TextTrimming>(xmlElement, "TextTrimming", TextTrimming.None);
            textBlock.TextWrapping = ParseXmlAttribute<TextWrapping>(xmlElement, "TextWrapping", TextWrapping.NoWrap);
            textBlock.TextDecorations = GetTextDecorationsFromXElement(xmlElement);

            string? fontFamilyAttr = xmlElement.Attribute("FontFamily")?.Value;
            if (!string.IsNullOrEmpty(fontFamilyAttr))
            {
                string resolvedUri = ThemeFontManager.ResolveFontUri(fontFamilyAttr, dialog.ThemeDir);
                textBlock.FontFamily = new Avalonia.Media.FontFamily(resolvedUri);
            }

            var padding = GetThicknessFromXElement(xmlElement, "Padding");
            if (padding is Thickness p)
                textBlock.Padding = p;
        }

        private static Control HandleXmlElement_TextBlock(CustomDialog dialog, XElement xmlElement)
        {
            var textBlock = new TextBlock();
            HandleXmlElement_TextBlock_Base(dialog, textBlock, xmlElement);

            textBlock.Text = GetTranslatedText(xmlElement.Attribute("Text")?.Value);

            if (xmlElement.Attribute("Name")?.Value == "StatusText")
            {
                textBlock.Bind(TextBlock.TextProperty, new Binding("Message"));
            }

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
            HandleXmlElement_FrameworkElement(dialog, image, xmlElement);

            image.Stretch = ParseXmlAttribute<Stretch>(xmlElement, "Stretch", Stretch.Uniform);
            image.StretchDirection = ParseXmlAttribute<StretchDirection>(xmlElement, "StretchDirection", StretchDirection.Both);

            var imageData = GetImageSourceData(dialog, "Source", xmlElement);

            if (imageData.IsIcon)
            {
                image.Bind(Image.SourceProperty, new Binding("Icon"));
            }
            else if (imageData.Uri != null)
            {
                bool isAnimated = ParseXmlAttribute<bool>(xmlElement, "IsAnimated", false);

                if (isAnimated)
                {
                    image.SetValue(ImageBehavior.AnimatedSourceProperty, imageData.Uri);

                    var repeat = ParseXmlAttribute<RepeatBehavior>(xmlElement, "RepeatBehavior", RepeatBehavior.Forever);
                    image.SetValue(ImageBehavior.RepeatBehaviorProperty, repeat);
                }
                else
                {
                    image.Source = new Bitmap(imageData.Uri.LocalPath);
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
            HandleXmlElement_FrameworkElement(dialog, grid, xmlElement);

            bool rowsSet = false;
            bool columnsSet = false;

            foreach (var element in xmlElement.Elements())
            {
                if (element.Name == "Grid.RowDefinitions")
                {
                    if (rowsSet)
                        throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMultipleDefinitions", "Grid", "RowDefinitions");
                    rowsSet = true;

                    HandleXmlElement_Grid_RowDefinitions(grid, dialog, element);
                }
                else if (element.Name == "Grid.ColumnDefinitions")
                {
                    if (columnsSet)
                        throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMultipleDefinitions", "Grid", "ColumnDefinitions");
                    columnsSet = true;

                    HandleXmlElement_Grid_ColumnDefinitions(grid, dialog, element);
                }
                else if (element.Name.ToString().StartsWith("Grid."))
                {
                    continue;
                }
                else
                {
                    var uiElement = HandleXml<Control>(dialog, element);
                    if (uiElement != null)
                        grid.Children.Add(uiElement);
                }
            }

            return grid;
        }

        private static StackPanel HandleXmlElement_StackPanel(CustomDialog dialog, XElement xmlElement)
        {
            var stackPanel = new StackPanel();
            HandleXmlElement_FrameworkElement(dialog, stackPanel, xmlElement);

            stackPanel.Orientation = ParseXmlAttribute<Orientation>(xmlElement, "Orientation", Orientation.Vertical);

            foreach (var element in xmlElement.Elements())
            {
                var uiElement = HandleXml<Control>(dialog, element);
                if (uiElement != null)
                    stackPanel.Children.Add(uiElement);
            }

            return stackPanel;
        }

        private static Border HandleXmlElement_Border(CustomDialog dialog, XElement xmlElement)
        {
            var border = new Border();
            HandleXmlElement_FrameworkElement(dialog, border, xmlElement);

            ApplyBrush_Control(dialog, border, "Background", Border.BackgroundProperty, xmlElement);
            ApplyBrush_Control(dialog, border, "BorderBrush", Border.BorderBrushProperty, xmlElement);

            var borderThickness = GetThicknessFromXElement(xmlElement, "BorderThickness");
            if (borderThickness is Thickness bt)
                border.BorderThickness = bt;

            var padding = GetThicknessFromXElement(xmlElement, "Padding");
            if (padding is Thickness p)
                border.Padding = p;

            var cornerRadius = GetCornerRadiusFromXElement(xmlElement, "CornerRadius");
            if (cornerRadius is CornerRadius cr)
                border.CornerRadius = cr;

            var children = xmlElement.Elements().Where(x => !x.Name.ToString().StartsWith("Border."));
            if (children.Any())
            {
                if (children.Count() > 1)
                    throw new CustomThemeException("CustomTheme.Errors.ElementMultipleChildren", "Border");

                border.Child = HandleXml<Control>(dialog, children.First());
            }

            return border;
        }
        #endregion
    }
}