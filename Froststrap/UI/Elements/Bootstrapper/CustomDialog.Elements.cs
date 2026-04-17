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
using FluentAvalonia.UI.Controls;
using Froststrap.UI.Elements.Controls;
using System.Xml.Linq;

namespace Froststrap.UI.Elements.Bootstrapper
{
    public partial class CustomDialog
    {
        #region Transformation
        private static Transform HandleXmlElement_ScaleTransform(CustomDialog dialog, XElement xmlElement)
        {
            var st = new ScaleTransform();

            st.ScaleX = ParseXmlAttribute<double>(xmlElement, "ScaleX", 1);
            st.ScaleY = ParseXmlAttribute<double>(xmlElement, "ScaleY", 1);

            return st;
        }

        private static Transform HandleXmlElement_SkewTransform(CustomDialog dialog, XElement xmlElement)
        {
            var st = new SkewTransform();

            st.AngleX = ParseXmlAttribute<double>(xmlElement, "AngleX", 0);
            st.AngleY = ParseXmlAttribute<double>(xmlElement, "AngleY", 0);

            return st;
        }

        private static Transform HandleXmlElement_RotateTransform(CustomDialog dialog, XElement xmlElement)
        {
            var rt = new RotateTransform();

            rt.Angle = ParseXmlAttribute<double>(xmlElement, "Angle", 0);

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
        private static BlurEffect HandleXmlElement_BlurEffect(CustomDialog dialog, XElement xmlElement)
        {
            var effect = new BlurEffect();

            effect.Radius = ParseXmlAttribute<double>(xmlElement, "Radius", 5);

            return effect;
        }

        private static DropShadowEffect HandleXmlElement_DropShadowEffect(CustomDialog dialog, XElement xmlElement)
        {
            var effect = new DropShadowEffect();

            effect.BlurRadius = ParseXmlAttribute<double>(xmlElement, "BlurRadius", 5);
            effect.Opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1);

            double direction = ParseXmlAttribute<double>(xmlElement, "Direction", 315);
            double depth = ParseXmlAttribute<double>(xmlElement, "ShadowDepth", 5);

            double radians = direction * (Math.PI / 180.0);
            effect.OffsetX = Math.Cos(radians) * depth;
            effect.OffsetY = -Math.Sin(radians) * depth;

            var color = GetColorFromXElement(xmlElement, "Color");
            if (color is Color c)
                effect.Color = c;

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

            var viewbox = GetRectFromXElement(xmlElement, "Viewbox");
            if (viewbox is Rect rVb)
                imageBrush.SourceRect = new RelativeRect(rVb, RelativeUnit.Relative);

            var viewport = GetRectFromXElement(xmlElement, "Viewport");
            if (viewport is Rect rVp)
                imageBrush.DestinationRect = new RelativeRect(rVp, RelativeUnit.Relative);

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
            if (color is Color c)
                gs.Color = c;

            gs.Offset = ParseXmlAttribute<double>(xmlElement, "Offset", 0.0);

            return gs;
        }

        private static Brush HandleXmlElement_LinearGradientBrush(CustomDialog dialog, XElement xmlElement)
        {
            var brush = new LinearGradientBrush();
            HandleXml_Brush(brush, xmlElement);

            if (GetPointFromXElement(xmlElement, "StartPoint") is Point start)
                brush.StartPoint = new RelativePoint(start, RelativeUnit.Relative);

            if (GetPointFromXElement(xmlElement, "EndPoint") is Point end)
                brush.EndPoint = new RelativePoint(end, RelativeUnit.Relative);

            brush.SpreadMethod = ParseXmlAttribute<GradientSpreadMethod>(xmlElement, "SpreadMethod", GradientSpreadMethod.Pad);

            foreach (var child in xmlElement.Elements())
                brush.GradientStops.Add(HandleXml<GradientStop>(dialog, child));

            return brush;
        }

        private static void ApplyBrush_UIElement(CustomDialog dialog, AvaloniaObject uiElement, string name, AvaloniaProperty property, XElement xmlElement)
        {
            object? brushAttr = GetBrushFromXElement(xmlElement, name);
            if (brushAttr is Brush b)
            {
                uiElement.SetValue(property, b);
                return;
            }
            else if (brushAttr is string s)
            {
                if (uiElement is StyledElement styled)
                    styled.Bind(property, new DynamicResourceExtension(s));
                return;
            }

            var brushElement = xmlElement.Element($"{xmlElement.Name.LocalName}.{name}");
            if (brushElement == null)
                return;

            if (brushElement.FirstNode is XElement first)
            {
                var brush = HandleXml<Brush>(dialog, first);
                uiElement.SetValue(property, brush);
            }
            else
            {
                throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissingChild", xmlElement.Name, name);
            }
        }
        #endregion

        #region Shapes

        //TOOD: Fix shape being invisible
        private static void HandleXmlElement_Shape(CustomDialog dialog, Shape shape, XElement xmlElement)
        {
            HandleXmlElement_FrameworkElement(dialog, shape, xmlElement);

            ApplyBrush_UIElement(dialog, shape, "Fill", Shape.FillProperty, xmlElement);
            ApplyBrush_UIElement(dialog, shape, "Stroke", Shape.StrokeProperty, xmlElement);

            shape.Stretch = ParseXmlAttribute<Stretch>(xmlElement, "Stretch", Stretch.Fill);
            shape.StrokeDashOffset = ParseXmlAttribute<double>(xmlElement, "StrokeDashOffset", 0);
            shape.StrokeJoin = ParseXmlAttribute<PenLineJoin>(xmlElement, "StrokeLineJoin", PenLineJoin.Miter);
            shape.StrokeMiterLimit = ParseXmlAttribute<double>(xmlElement, "StrokeMiterLimit", 10);
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
            if (name != null)
            {
                if (dialog.UsedNames.Contains(name))
                    throw new Exception($"{xmlElement.Name} has duplicate name {name}");

                dialog.UsedNames.Add(name);
                uiElement.Name = name;
            }

            bool? isVisibleAttr = ParseXmlAttributeNullable<bool>(xmlElement, "IsVisible");
            string? visibilityAttr = xmlElement.Attribute("Visibility")?.Value?.ToLower();

            if (isVisibleAttr.HasValue)
            {
                uiElement.IsVisible = isVisibleAttr.Value;
            }
            else if (!string.IsNullOrEmpty(visibilityAttr))
            {
                switch (visibilityAttr)
                {
                    case "visible":
                        uiElement.IsVisible = true;
                        break;
                    case "hidden":
                        uiElement.IsVisible = true;
                        uiElement.Opacity = 0;
                        break;
                    case "collapsed":
                        uiElement.IsVisible = false;
                        break;
                }
            }
            else
            {
                uiElement.IsVisible = true;
            }

            uiElement.IsEnabled = ParseXmlAttribute<bool>(xmlElement, "IsEnabled", true);

            if (GetThicknessFromXElement(xmlElement, "Margin") is Thickness margin)
                uiElement.Margin = margin;

            uiElement.Height = ParseXmlAttribute<double>(xmlElement, "Height", double.NaN);
            uiElement.Width = ParseXmlAttribute<double>(xmlElement, "Width", double.NaN);

            uiElement.HorizontalAlignment = ParseXmlAttribute<HorizontalAlignment>(xmlElement, "HorizontalAlignment", HorizontalAlignment.Left);
            uiElement.VerticalAlignment = ParseXmlAttribute<VerticalAlignment>(xmlElement, "VerticalAlignment", VerticalAlignment.Top);

            uiElement.Opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1);

            if (GetPointFromXElement(xmlElement, "RenderTransformOrigin") is Point origin)
                uiElement.RenderTransformOrigin = new RelativePoint(origin, RelativeUnit.Relative);

            int zIndex = ParseXmlAttribute<int>(xmlElement, "ZIndex", -1);

            if (zIndex == -1)
                zIndex = ParseXmlAttribute<int>(xmlElement, "Panel.ZIndex", 0);

            uiElement.ZIndex = Math.Clamp(zIndex, 0, 1000);

            Grid.SetRow(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.Row", 0));
            Grid.SetRowSpan(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.RowSpan", 1));
            Grid.SetColumn(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.Column", 0));
            Grid.SetColumnSpan(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.ColumnSpan", 1));

            ApplyTransformations_UIElement(dialog, uiElement, xmlElement);
            ApplyEffects_UIElement(dialog, uiElement, xmlElement);
        }

        private static void HandleXmlElement_Control(CustomDialog dialog, TemplatedControl uiElement, XElement xmlElement)
        {
            HandleXmlElement_FrameworkElement(dialog, uiElement, xmlElement);

            if (GetThicknessFromXElement(xmlElement, "Padding") is Thickness padding)
                uiElement.Padding = padding;

            if (GetThicknessFromXElement(xmlElement, "BorderThickness") is Thickness bt)
                uiElement.BorderThickness = bt;

            ApplyBrush_UIElement(dialog, uiElement, "Foreground", TemplatedControl.ForegroundProperty, xmlElement);
            ApplyBrush_UIElement(dialog, uiElement, "Background", TemplatedControl.BackgroundProperty, xmlElement);
            ApplyBrush_UIElement(dialog, uiElement, "BorderBrush", TemplatedControl.BorderBrushProperty, xmlElement);

            if (ParseXmlAttributeNullable<double>(xmlElement, "FontSize") is double fs)
                uiElement.FontSize = fs;

            uiElement.FontWeight = GetFontWeightFromXElement(xmlElement);
            uiElement.FontStyle = GetFontStyleFromXElement(xmlElement);

            //TODO: Fix Font Faaily
            string? fontFamilyAttr = xmlElement.Attribute("FontFamily")?.Value;
            if (!string.IsNullOrEmpty(fontFamilyAttr))
            {
                if (fontFamilyAttr.StartsWith("theme://"))
                {
                    string fullPath = GetFullPath(dialog, fontFamilyAttr)!;

                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        try
                        {
                            var uri = new Uri(fullPath, UriKind.Absolute);
                            string fontName = fontFamilyAttr.Split('#').Last();
                            uiElement.FontFamily = new Avalonia.Media.FontFamily(uri, fontName);
                        }
                        catch (Exception)
                        {
                            uiElement.FontFamily = new Avalonia.Media.FontFamily("Arial");
                        }
                    }
                }
                else
                {
                    uiElement.FontFamily = new Avalonia.Media.FontFamily(fontFamilyAttr);
                }
            }
        }

        private static Control HandleXmlElement_BloxstrapCustomBootstrapper(CustomDialog dialog, XElement xmlElement)
        {
            HandleXmlElement_Control(dialog, dialog, xmlElement);

            bool? isVisibleAttr = ParseXmlAttributeNullable<bool>(xmlElement, "IsVisible");
            string? visibilityAttr = xmlElement.Attribute("Visibility")?.Value?.ToLower();

            if (isVisibleAttr.HasValue)
            {
                dialog.IsVisible = isVisibleAttr.Value;
            }
            else if (!string.IsNullOrEmpty(visibilityAttr))
            {
                switch (visibilityAttr)
                {
                    case "visible":
                        dialog.IsVisible = true;
                        break;
                    case "hidden":
                        dialog.IsVisible = true;
                        dialog.Opacity = 0;
                        break;
                    case "collapsed":
                        dialog.IsVisible = false;
                        break;
                }
            }
            else
            {
                dialog.IsVisible = true;
            }

            xmlElement.SetAttributeValue("IsEnabled", "True");

            dialog.Opacity = 1;

            dialog.ElementGrid.RenderTransform = dialog.RenderTransform;
            dialog.RenderTransform = null;

            dialog.ElementGrid.RenderTransform = dialog.RenderTransform;
            dialog.RenderTransform = null;

            dialog.ElementGrid.Effect = dialog.Effect;
            dialog.Effect = null;

            var theme = ParseXmlAttribute<Theme>(xmlElement, "Theme", Enums.Theme.Default);
            if (theme == Enums.Theme.Default)
                theme = App.Settings.Prop.Theme;

            var avaloniaTheme = theme.GetFinal() == Enums.Theme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

            dialog.RequestedThemeVariant = avaloniaTheme;

            dialog.Resources.MergedDictionaries.Clear();

            dialog.ElementGrid.Margin = dialog.Margin;

            dialog.Margin = new Thickness(0);
            dialog.Padding = new Thickness(0);

            string? title = xmlElement.Attribute("Title")?.Value ?? "Bloxstrap";
            dialog.Title = title;

            bool ignoreTitleBarInset = ParseXmlAttribute<bool>(xmlElement, "IgnoreTitleBarInset", false);
            if (ignoreTitleBarInset)
            {
                Grid.SetRow(dialog.ElementGrid, 0);
                Grid.SetRowSpan(dialog.ElementGrid, 2);
            }

            return new Control();
        }

        private static Control HandleXmlElement_BloxstrapCustomBootstrapper_Fake(CustomDialog dialog, XElement xmlElement)
        {
            throw new CustomThemeException("CustomTheme.Errors.ElementInvalidChild", xmlElement.Parent!.Name.LocalName, xmlElement.Name.LocalName);
        }

        private static Control HandleXmlElement_TitleBar(CustomDialog dialog, XElement xmlElement)
        {
            HandleXmlElement_Control(dialog, dialog.RootTitleBar, xmlElement);

            xmlElement.SetAttributeValue("Name", "TitleBar");
            xmlElement.SetAttributeValue("IsEnabled", "True");

            dialog.RootTitleBar.RenderTransform = null;

            dialog.RootTitleBar.Effect = null;

            dialog.RootTitleBar.ZIndex = 1001;

            dialog.RootTitleBar.Height = double.NaN;
            dialog.RootTitleBar.Width = double.NaN;
            dialog.RootTitleBar.HorizontalAlignment = HorizontalAlignment.Stretch;
            dialog.RootTitleBar.Margin = new Thickness(0, 0, 0, 0);

            dialog.RootTitleBar.ShowMinimize = ParseXmlAttribute<bool>(xmlElement, "ShowMinimize", true);
            dialog.RootTitleBar.ShowClose = ParseXmlAttribute<bool>(xmlElement, "ShowClose", true);

            string? title = xmlElement.Attribute("Title")?.Value?.ToString() ?? "Bloxstrap";
            dialog.RootTitleBar.Title = title;

            return new Control();
        }

        private static Control HandleXmlElement_Button(CustomDialog dialog, XElement xmlElement)
        {
            var button = new Button();

            HandleXmlElement_Control(dialog, button, xmlElement);

            button.Content = GetContentFromXElement(dialog, xmlElement);

            if (xmlElement.Attribute("Name")?.Value == "CancelButton")
            {
                button.Bind(Button.IsEnabledProperty, new Binding("CancelEnabled")
                {
                    Mode = BindingMode.OneWay
                });

                button.Bind(Button.CommandProperty, new Binding("CancelInstallCommand"));
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

            HandleXmlElement_RangeBase(dialog, progressBar, xmlElement);

            progressBar.IsIndeterminate = ParseXmlAttribute<bool>(xmlElement, "IsIndeterminate", false);

            object? cornerRadius = GetCornerRadiusFromXElement(xmlElement, "CornerRadius");
            if (cornerRadius is CornerRadius cr)
                progressBar.CornerRadius = cr;


            if (xmlElement.Attribute("Name")?.Value == "PrimaryProgressBar")
            {
                progressBar.Bind(ProgressBar.IsIndeterminateProperty, new Binding("ProgressIndeterminate")
                {
                    Mode = BindingMode.OneWay
                });

                progressBar.Bind(ProgressBar.MaximumProperty, new Binding("ProgressMaximum")
                {
                    Mode = BindingMode.OneWay
                });

                progressBar.Bind(ProgressBar.ValueProperty, new Binding("ProgressValue")
                {
                    Mode = BindingMode.OneWay
                });
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
            HandleXmlElement_FrameworkElement(dialog, textBlock, xmlElement);

            ApplyBrush_UIElement(dialog, textBlock, "Foreground", TextBlock.ForegroundProperty, xmlElement);
            ApplyBrush_UIElement(dialog, textBlock, "Background", TextBlock.BackgroundProperty, xmlElement);

            if (ParseXmlAttributeNullable<double>(xmlElement, "FontSize") is double fontSize)
                textBlock.FontSize = fontSize;

            textBlock.FontWeight = GetFontWeightFromXElement(xmlElement);
            textBlock.FontStyle = GetFontStyleFromXElement(xmlElement);

            textBlock.LineHeight = ParseXmlAttribute<double>(xmlElement, "LineHeight", double.NaN);


            textBlock.TextAlignment = ParseXmlAttribute<TextAlignment>(xmlElement, "TextAlignment", TextAlignment.Center);
            textBlock.TextTrimming = GetTextTrimmingFromXElement(xmlElement);
            textBlock.TextWrapping = ParseXmlAttribute<TextWrapping>(xmlElement, "TextWrapping", TextWrapping.NoWrap);

            textBlock.TextDecorations = GetTextDecorationsFromXElement(xmlElement);

            //TODO: Fix Font Faaily
            string? fontFamilyAttr = xmlElement.Attribute("FontFamily")?.Value;
            if (!string.IsNullOrEmpty(fontFamilyAttr))
            {
                if (fontFamilyAttr.StartsWith("theme://"))
                {
                    string fullPath = GetFullPath(dialog, fontFamilyAttr)!;

                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        try
                        {
                            var uri = new Uri(fullPath, UriKind.Absolute);
                            string fontName = fontFamilyAttr.Split('#').Last();
                            textBlock.FontFamily = new Avalonia.Media.FontFamily(uri, fontName);
                        }
                        catch (Exception)
                        {
                            textBlock.FontFamily = new Avalonia.Media.FontFamily("Arial");
                        }
                    }
                }
                else
                {
                    textBlock.FontFamily = new Avalonia.Media.FontFamily(fontFamilyAttr);
                }
            }


            if (GetThicknessFromXElement(xmlElement, "Padding") is Thickness padding)
                textBlock.Padding = padding;
        }

        private static Control HandleXmlElement_TextBlock(CustomDialog dialog, XElement xmlElement)
        {
            var textBlock = new TextBlock();
            HandleXmlElement_TextBlock_Base(dialog, textBlock, xmlElement);

            textBlock.Text = GetTranslatedText(xmlElement.Attribute("Text")?.Value);

            if (xmlElement.Attribute("Name")?.Value == "StatusText")
            {
                textBlock.Bind(TextBlock.TextProperty, new Binding("Message")
                {
                    Mode = BindingMode.OneWay
                });
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

            image.HorizontalAlignment = ParseXmlAttribute<HorizontalAlignment>(xmlElement, "HorizontalAlignment", HorizontalAlignment.Center);
            image.VerticalAlignment = ParseXmlAttribute<VerticalAlignment>(xmlElement, "VerticalAlignment", VerticalAlignment.Center);

            image.Stretch = ParseXmlAttribute<Stretch>(xmlElement, "Stretch", Stretch.Uniform);
            image.StretchDirection = ParseXmlAttribute<StretchDirection>(xmlElement, "StretchDirection", StretchDirection.Both);

            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

            var sourceData = GetImageSourceData(dialog, "Source", xmlElement);
            bool isAnimated = ParseXmlAttribute<bool>(xmlElement, "IsAnimated", false);

            if (sourceData.IsIcon)
            {
                image.Bind(Image.SourceProperty, new Binding("Icon") { Mode = BindingMode.OneWay });
            }
            else if (sourceData.Uri != null)
            {
                if (isAnimated)
                {
                    try
                    {
                        //TODO: Fix issue causing it to run in background after stopping preview
                        image.SetValue(AnimatedImage.Avalonia.ImageBehavior.AnimatedSourceProperty, sourceData.Uri);
                    }
                    catch (Exception)
                    {
                        image.Source = new Bitmap(sourceData.Uri.LocalPath);
                    }
                }
                else
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
                stackPanel.Children.Add(uiElement);
            }

            return stackPanel;
        }

        //TODO: Fix border being invisible
        private static Border HandleXmlElement_Border(CustomDialog dialog, XElement xmlElement)
        {
            var border = new Border();
            HandleXmlElement_FrameworkElement(dialog, border, xmlElement);

            ApplyBrush_UIElement(dialog, border, "Background", Border.BackgroundProperty, xmlElement);
            ApplyBrush_UIElement(dialog, border, "BorderBrush", Border.BorderBrushProperty, xmlElement);

            object? borderThickness = GetThicknessFromXElement(xmlElement, "BorderThickness");
            if (borderThickness != null)
                border.BorderThickness = (Thickness)borderThickness;

            object? padding = GetThicknessFromXElement(xmlElement, "Padding");
            if (padding != null)
                border.Padding = (Thickness)padding;

            object? cornerRadius = GetCornerRadiusFromXElement(xmlElement, "CornerRadius");
            if (cornerRadius != null)
                border.CornerRadius = (CornerRadius)cornerRadius;

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