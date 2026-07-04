using AnimatedImage.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
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
        private readonly List<Image> _animatedImages = [];

        #region Transformation
        private static ScaleTransform HandleXmlElement_ScaleTransform(CustomDialog dialog, XElement xmlElement)
        {
            var st = new ScaleTransform
            {
                ScaleX = ParseXmlAttribute<double>(xmlElement, "ScaleX", 1),
                ScaleY = ParseXmlAttribute<double>(xmlElement, "ScaleY", 1)
            };

            return st;
        }

        private static SkewTransform HandleXmlElement_SkewTransform(CustomDialog dialog, XElement xmlElement)
        {
            return new SkewTransform
            {
                AngleX = ParseXmlAttribute<double>(xmlElement, "AngleX", 0),
                AngleY = ParseXmlAttribute<double>(xmlElement, "AngleY", 0)
            };
        }

        private static RotateTransform HandleXmlElement_RotateTransform(CustomDialog dialog, XElement xmlElement)
        {
            return new RotateTransform
            {
                Angle = ParseXmlAttribute<double>(xmlElement, "Angle", 0)
            };
        }

        private static TranslateTransform HandleXmlElement_TranslateTransform(CustomDialog dialog, XElement xmlElement)
        {
            return new TranslateTransform
            {
                X = ParseXmlAttribute<double>(xmlElement, "X", 0),
                Y = ParseXmlAttribute<double>(xmlElement, "Y", 0)
            };
        }
        #endregion

        #region Effects
        private static BlurEffect HandleXmlElement_BlurEffect(CustomDialog dialog, XElement xmlElement)
        {
            return new BlurEffect
            {
                Radius = ParseXmlAttribute<double>(xmlElement, "Radius", 5)
            };
        }

        private static object HandleXmlElement_DropShadowEffect(CustomDialog dialog, XElement xmlElement)
        {
            double blurRadius = ParseXmlAttribute<double>(xmlElement, "BlurRadius", 5);
            double direction = ParseXmlAttribute<double>(xmlElement, "Direction", 315);
            double opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1);
            double shadowDepth = ParseXmlAttribute<double>(xmlElement, "ShadowDepth", 5);

            double angleRad = direction * (Math.PI / 180.0);
            double offsetX = shadowDepth * Math.Cos(angleRad);
            double offsetY = -shadowDepth * Math.Sin(angleRad);

            var colorObj = GetColorFromXElement(xmlElement, "Color");
            Color color = colorObj is Color c ? c : Colors.Black;

            var finalColor = Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B);

            return new BoxShadows(new BoxShadow
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                Blur = blurRadius,
                Spread = 0,
                Color = finalColor
            });
        }
        #endregion

        #region Brushes
        private static void HandleXml_Brush(Brush brush, XElement xmlElement)
        {
            brush.Opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1.0);
        }

        private static SolidColorBrush HandleXmlElement_SolidColorBrush(CustomDialog dialog, XElement xmlElement)
        {
            var brush = new SolidColorBrush();
            HandleXml_Brush(brush, xmlElement);

            object? color = GetColorFromXElement(xmlElement, "Color");
            if (color is Color c)
                brush.Color = c;

            return brush;
        }

        private static ImageBrush HandleXmlElement_ImageBrush(CustomDialog dialog, XElement xmlElement)
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
                Binding binding = new("Icon") { Mode = BindingMode.OneWay };
                imageBrush.Bind(ImageBrush.SourceProperty, binding);
            }
            else if (sourceData.Path != null)
            {
                Bitmap bitmapImage;
                try
                {
                    bitmapImage = new Bitmap(sourceData.Path);
                }
                catch (Exception ex)
                {
                    throw new CustomThemeException(ex, "CustomTheme.Errors.ElementTypeCreationFailed", "Image", "Bitmap", ex.Message);
                }

                imageBrush.Source = bitmapImage;
            }

            return imageBrush;
        }

        private static Avalonia.Media.GradientStop HandleXmlElement_GradientStop(CustomDialog dialog, XElement xmlElement)
        {
            var gs = new Avalonia.Media.GradientStop();

            object? color = GetColorFromXElement(xmlElement, "Color");
            if (color is Color c)
                gs.Color = c;

            gs.Offset = ParseXmlAttribute<double>(xmlElement, "Offset", 0.0);

            return gs;
        }

        private static LinearGradientBrush HandleXmlElement_LinearGradientBrush(CustomDialog dialog, XElement xmlElement)
        {
            var brush = new LinearGradientBrush();

            // Using Point.Parse makes StartPoint and EndPoint not work
            // Using normal RelivePoints.Parse breaks the gradient and offset and Start/End points
            static RelativePoint ParseRelativePoint(string value)
            {
                string[] parts = value.Split(',');
                double x = double.Parse(parts[0], CultureInfo.InvariantCulture);
                double y = double.Parse(parts[1], CultureInfo.InvariantCulture);
                return new RelativePoint(x, y, RelativeUnit.Relative);
            }

            string? startPointStr = xmlElement.Attribute("StartPoint")?.Value;
            if (startPointStr != null)
                brush.StartPoint = ParseRelativePoint(startPointStr);

            string? endPointStr = xmlElement.Attribute("EndPoint")?.Value;
            if (endPointStr != null)
                brush.EndPoint = ParseRelativePoint(endPointStr);

            foreach (var child in xmlElement.Elements())
            {
                if (child.Name.LocalName == "GradientStop")
                {
                    string? colorAttr = child.Attribute("Color")?.Value;
                    string? offsetAttr = child.Attribute("Offset")?.Value;

                    if (colorAttr != null && offsetAttr != null)
                    {
                        var color = Color.Parse(colorAttr);
                        var offset = double.Parse(offsetAttr, CultureInfo.InvariantCulture);

                        brush.GradientStops.Add(new Avalonia.Media.GradientStop(color, offset));
                    }
                }
            }

            return brush;
        }
        
        private static void ApplyBrush_UIElement(CustomDialog dialog, AvaloniaObject uiElement, string name, AvaloniaProperty dependencyProperty, XElement xmlElement)
        {
            object? brushAttr = GetBrushFromXElement(xmlElement, name);
            if (brushAttr is IBrush brush)
            {
                uiElement.SetValue(dependencyProperty, brush);
                return;
            }
            else if (brushAttr is string resourceKey)
            {
                uiElement.Bind(dependencyProperty, dialog.GetResourceObservable(resourceKey));
                return;
            }

            var brushElement = xmlElement.Element($"{xmlElement.Name}.{name}");
            if (brushElement == null)
                return;

            var first = brushElement.Elements().FirstOrDefault();
            _ = first ?? throw new CustomThemeException("CustomTheme.Errors.ElementAttributeMissingChild", xmlElement.Name, name);

            var generatedBrush = HandleXml<IBrush>(dialog, first);
            uiElement.SetValue(dependencyProperty, generatedBrush);
        }
        #endregion

        #region Shapes
        private static void HandleXmlElement_Shape(CustomDialog dialog, Shape shape, XElement xmlElement)
        {
            HandleXmlElement_FrameworkElement(dialog, shape, xmlElement);

            ApplyBrush_UIElement(dialog, shape, "Fill", Shape.FillProperty, xmlElement);
            ApplyBrush_UIElement(dialog, shape, "Stroke", Shape.StrokeProperty, xmlElement);

            shape.Stretch = ParseXmlAttribute<Stretch>(xmlElement, "Stretch", Stretch.Fill);
            shape.StrokeDashOffset = ParseXmlAttribute<double>(xmlElement, "StrokeDashOffset", 0);
            shape.StrokeLineCap = ParseXmlAttribute<PenLineCap>(xmlElement, "StrokeLineCap", PenLineCap.Flat);
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

            double x1 = ParseXmlAttribute<double>(xmlElement, "X1", 0);
            double y1 = ParseXmlAttribute<double>(xmlElement, "Y1", 0);
            double x2 = ParseXmlAttribute<double>(xmlElement, "X2", 0);
            double y2 = ParseXmlAttribute<double>(xmlElement, "Y2", 0);

            line.StartPoint = new Point(x1, y1);
            line.EndPoint = new Point(x2, y2);

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

                if (string.IsNullOrEmpty(uiElement.Name))
                {
                    uiElement.Name = name;
                }
            }

            uiElement.IsEnabled = ParseXmlAttribute<bool>(xmlElement, "IsEnabled", true);
            uiElement.Height = ParseXmlAttribute<double>(xmlElement, "Height", double.NaN);
            uiElement.Width = ParseXmlAttribute<double>(xmlElement, "Width", double.NaN);
            uiElement.Margin = (Thickness)(GetThicknessFromXElement(xmlElement, "Margin") ?? new Thickness(0));

            uiElement.HorizontalAlignment = ParseXmlAttribute<HorizontalAlignment>(xmlElement, "HorizontalAlignment", HorizontalAlignment.Left);
            uiElement.VerticalAlignment = ParseXmlAttribute<VerticalAlignment>(xmlElement, "VerticalAlignment", VerticalAlignment.Top);

            uiElement.Opacity = ParseXmlAttribute<double>(xmlElement, "Opacity", 1);
            ApplyBrush_UIElement(dialog, uiElement, "OpacityMask", Visual.OpacityMaskProperty, xmlElement);

            object? renderTransformOrigin = GetPointFromXElement(xmlElement, "RenderTransformOrigin");
            if (renderTransformOrigin is RelativePoint origin)
                uiElement.RenderTransformOrigin = origin;

            uiElement.ZIndex = ParseXmlAttributeClamped(xmlElement, "ZIndex", defaultValue: 0, min: 0, max: 1000);

            Grid.SetRow(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.Row", 0));
            Grid.SetRowSpan(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.RowSpan", 1));
            Grid.SetColumn(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.Column", 0));
            Grid.SetColumnSpan(uiElement, ParseXmlAttribute<int>(xmlElement, "Grid.ColumnSpan", 1));

            ApplyTransformations_Control(dialog, uiElement, xmlElement);
            ApplyEffects_Control(dialog, uiElement, xmlElement);

            string? visibility = xmlElement.Attribute("Visibility")?.Value;
            if (!string.IsNullOrEmpty(visibility))
            {
                switch (visibility.ToLower())
                {
                    case "collapsed":
                        uiElement.IsVisible = false;
                        break;
                    case "hidden":
                        uiElement.IsVisible = true;
                        uiElement.Opacity = 0;
                        uiElement.IsHitTestVisible = false;
                        break;
                    case "visible":
                    default:
                        uiElement.IsVisible = true;
                        break;
                }
            }
        }

        private static void HandleXmlElement_Control(CustomDialog dialog, Control uiElement, XElement xmlElement)
        {
            HandleXmlElement_FrameworkElement(dialog, uiElement, xmlElement);

            if (uiElement is TemplatedControl templatedControl)
            {
                object? padding = GetThicknessFromXElement(xmlElement, "Padding");
                if (padding != null)
                    templatedControl.Padding = (Thickness)padding;

                object? borderThickness = GetThicknessFromXElement(xmlElement, "BorderThickness");
                if (borderThickness != null)
                    templatedControl.BorderThickness = (Thickness)borderThickness;

                ApplyBrush_UIElement(dialog, templatedControl, "Foreground", TemplatedControl.ForegroundProperty, xmlElement);
                ApplyBrush_UIElement(dialog, templatedControl, "Background", TemplatedControl.BackgroundProperty, xmlElement);
                ApplyBrush_UIElement(dialog, templatedControl, "BorderBrush", TemplatedControl.BorderBrushProperty, xmlElement);

                ApplyFontFamily(dialog, templatedControl, xmlElement);

                var fontSize = ParseXmlAttributeNullable<double>(xmlElement, "FontSize");
                if (fontSize is double fs)
                    templatedControl.FontSize = fs;

                templatedControl.FontWeight = GetFontWeightFromXElement(xmlElement);
                templatedControl.FontStyle = GetFontStyleFromXElement(xmlElement);
            }
        }

        private static DummyControl HandleXmlElement_BloxstrapCustomBootstrapper(CustomDialog dialog, XElement xmlElement)
        {
            xmlElement.SetAttributeValue("Visibility", "Collapsed");
            xmlElement.SetAttributeValue("IsEnabled", "True");
            HandleXmlElement_Control(dialog, dialog, xmlElement);

            dialog.Opacity = 1;

            dialog.ElementGrid.RenderTransform = dialog.RenderTransform;
            dialog.RenderTransform = null;

            var backdropType = ParseXmlAttribute<WindowsBackdrops>(xmlElement, "WindowBackdropType", WindowsBackdrops.None);

            List<WindowTransparencyLevel> transparencyLevels = [];

            switch (backdropType)
            {
                case WindowsBackdrops.Mica:
                    transparencyLevels.Add(WindowTransparencyLevel.Mica);
                    transparencyLevels.Add(WindowTransparencyLevel.AcrylicBlur);
                    transparencyLevels.Add(WindowTransparencyLevel.Blur);
                    break;

                case WindowsBackdrops.Acrylic:
                    transparencyLevels.Add(WindowTransparencyLevel.AcrylicBlur);
                    transparencyLevels.Add(WindowTransparencyLevel.Blur);
                    transparencyLevels.Add(WindowTransparencyLevel.Mica);
                    break;

                case WindowsBackdrops.Aero:
                    transparencyLevels.Add(WindowTransparencyLevel.Blur);
                    transparencyLevels.Add(WindowTransparencyLevel.AcrylicBlur);
                    break;

                case WindowsBackdrops.None:
                default:
                    transparencyLevels.Add(WindowTransparencyLevel.None);
                    break;
            }

            dialog.TransparencyLevelHint = transparencyLevels;

            var isLight = dialog.ActualThemeVariant == ThemeVariant.Light;

            if (backdropType != WindowsBackdrops.None)
            {
                byte alpha = backdropType switch
                {
                    WindowsBackdrops.Mica => (byte)200,
                    WindowsBackdrops.Acrylic => (byte)128,
                    WindowsBackdrops.Aero => (byte)64,
                    _ => (byte)180
                };

                if (dialog.Background != null && dialog.Background != Avalonia.Media.Brushes.Transparent)
                {
                    if (dialog.Background is SolidColorBrush solidBrush)
                    {
                        var originalColor = solidBrush.Color;
                        var newColor = Color.FromArgb(alpha, originalColor.R, originalColor.G, originalColor.B);
                        dialog.Background = new SolidColorBrush(newColor);
                    }
                    else if (dialog.Background is ImageBrush imageBrush)
                    {
                        imageBrush.Opacity = alpha / 255.0;
                    }
                    else if (dialog.Background is LinearGradientBrush linearGradient)
                    {
                        linearGradient.Opacity = alpha / 255.0;
                    }
                    else if (dialog.Background is RadialGradientBrush radialGradient)
                    {
                        radialGradient.Opacity = alpha / 255.0;
                    }
                }
                else
                {
                    var color = isLight
                        ? Color.FromArgb(alpha, 225, 225, 225)
                        : Color.FromArgb(alpha, 30, 30, 30);

                    dialog.Background = new SolidColorBrush(color);
                }
            }

            var theme = ParseXmlAttribute<Theme>(xmlElement, "Theme", Enums.Theme.Default);
            if (theme == Enums.Theme.Default)
                theme = App.Settings.Prop.Theme;

            var finalTheme = theme.GetFinal();
            dialog.RequestedThemeVariant = finalTheme == Enums.Theme.Light ? ThemeVariant.Light : ThemeVariant.Dark;

            dialog.ElementGrid.Margin = dialog.Margin;

            dialog.Margin = new Thickness(0, 0, 0, 0);
            dialog.Padding = new Thickness(0, 0, 0, 0);

            string? title = xmlElement.Attribute("Title")?.Value?.ToString() ?? "Froststrap";
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
            throw new Exception($"{xmlElement.Parent!.Name} cannot have a child of {xmlElement.Name}");
        }

        private static DummyControl HandleXmlElement_TitleBar(CustomDialog dialog, XElement xmlElement)
        {
            xmlElement.SetAttributeValue("Name", "TitleBar");
            xmlElement.SetAttributeValue("IsEnabled", "True");

            HandleXmlElement_Control(dialog, dialog.RootTitleBar, xmlElement);

            dialog.RootTitleBar.RenderTransform = null;
            dialog.RootTitleBar.ZIndex = 1001;

            dialog.RootTitleBar.Height = 32;
            dialog.RootTitleBar.Width = double.NaN;
            dialog.RootTitleBar.HorizontalAlignment = HorizontalAlignment.Stretch;
            dialog.RootTitleBar.Margin = new Thickness(0, 0, 0, 0);

            dialog.RootTitleBar.ShowMinimize = ParseXmlAttribute<bool>(xmlElement, "ShowMinimize", true);
            dialog.RootTitleBar.ShowMaximize = ParseXmlAttribute<bool>(xmlElement, "ShowMaximize", false);
            dialog.RootTitleBar.ShowClose = ParseXmlAttribute<bool>(xmlElement, "ShowClose", true);

            string? title = xmlElement.Attribute("Title")?.Value ?? "Froststrap";
            dialog.RootTitleBar.Title = title;

            if (OperatingSystem.IsMacOS())
            {
                dialog.WindowDecorations = WindowDecorations.Full;
                dialog.ExtendClientAreaToDecorationsHint = true;

                dialog.RootTitleBar.IsVisible = false;

                dialog.Title = title;

                Grid.SetRow(dialog.ElementGrid, 0);
                Grid.SetRowSpan(dialog.ElementGrid, 2);
            }

            return new DummyControl();
        }

        private static Button HandleXmlElement_Button(CustomDialog dialog, XElement xmlElement)
        {
            var button = new Button();
            HandleXmlElement_Control(dialog, button, xmlElement);

            button.Content = GetContentFromXElement(dialog, xmlElement);

            if (xmlElement.Attribute("Name")?.Value == "CancelButton")
            {
                Binding cancelEnabledBinding = new("CancelEnabled") { Mode = BindingMode.TwoWay };
                button.Bind(Button.IsEnabledProperty, cancelEnabledBinding);

                Binding cancelCommandBinding = new("CancelInstallCommand");
                button.Bind(Button.CommandProperty, cancelCommandBinding);
            }

            return button;
        }

        private static void HandleXmlElement_RangeBase(CustomDialog dialog, RangeBase rangeBase, XElement xmlElement)
        {
            HandleXmlElement_Control(dialog, rangeBase, xmlElement);

            rangeBase.Value = ParseXmlAttribute<double>(xmlElement, "Value", 0);
            rangeBase.Maximum = ParseXmlAttribute<double>(xmlElement, "Maximum", 100);
        }

        private static ProgressBar HandleXmlElement_ProgressBar(CustomDialog dialog, XElement xmlElement)
        {
            var progressBar = new ProgressBar();
            HandleXmlElement_RangeBase(dialog, progressBar, xmlElement);

            progressBar.IsIndeterminate = ParseXmlAttribute<bool>(xmlElement, "IsIndeterminate", false);

            object? cornerRadius = GetCornerRadiusFromXElement(xmlElement, "CornerRadius");
            if (cornerRadius != null)
                ProgressBarHelper.SetCornerRadius(progressBar, (CornerRadius)cornerRadius);

            object? indicatorCornerRadius = GetCornerRadiusFromXElement(xmlElement, "IndicatorCornerRadius");
            if (indicatorCornerRadius != null)
                ProgressBarHelper.SetIndicatorCornerRadius(progressBar, (CornerRadius)indicatorCornerRadius);

            if (xmlElement.Attribute("Name")?.Value == "PrimaryProgressBar")
            {
                Binding isIndeterminateBinding = new("ProgressIndeterminate") { Mode = BindingMode.OneWay };
                progressBar.Bind(ProgressBar.IsIndeterminateProperty, isIndeterminateBinding);

                Binding maximumBinding = new("ProgressMaximum") { Mode = BindingMode.OneWay };
                progressBar.Bind(ProgressBar.MaximumProperty, maximumBinding);

                Binding valueBinding = new("ProgressValue") { Mode = BindingMode.OneWay };
                progressBar.Bind(ProgressBar.ValueProperty, valueBinding);
            }

            return progressBar;
        }

        private static FluentAvalonia.UI.Controls.FAProgressRing HandleXmlElement_ProgressRing(CustomDialog dialog, XElement xmlElement)
        {
            var progressRing = new FluentAvalonia.UI.Controls.FAProgressRing();
            HandleXmlElement_RangeBase(dialog, progressRing, xmlElement);

            progressRing.IsIndeterminate = ParseXmlAttribute<bool>(xmlElement, "IsIndeterminate", false);

            if (xmlElement.Attribute("Name")?.Value == "PrimaryProgressRing")
            {
                Binding isIndeterminateBinding = new("ProgressIndeterminate") { Mode = BindingMode.OneWay };
                progressRing.Bind(FluentAvalonia.UI.Controls.FAProgressRing.IsIndeterminateProperty, isIndeterminateBinding);

                Binding maximumBinding = new("ProgressMaximum") { Mode = BindingMode.OneWay };
                progressRing.Bind(FluentAvalonia.UI.Controls.FAProgressRing.MaximumProperty, maximumBinding);

                Binding valueBinding = new("ProgressValue") { Mode = BindingMode.OneWay };
                progressRing.Bind(FluentAvalonia.UI.Controls.FAProgressRing.ValueProperty, valueBinding);
            }

            return progressRing;
        }

        private static void HandleXmlElement_TextBlock_Base(CustomDialog dialog, TextBlock textBlock, XElement xmlElement)
        {
            HandleXmlElement_FrameworkElement(dialog, textBlock, xmlElement);

            ApplyBrush_UIElement(dialog, textBlock, "Foreground", TextBlock.ForegroundProperty, xmlElement);
            ApplyBrush_UIElement(dialog, textBlock, "Background", TextBlock.BackgroundProperty, xmlElement);

            var fontSize = ParseXmlAttributeNullable<double>(xmlElement, "FontSize");
            if (fontSize is double value)
                textBlock.FontSize = value;

            textBlock.FontWeight = GetFontWeightFromXElement(xmlElement);
            textBlock.FontStyle = GetFontStyleFromXElement(xmlElement);

            textBlock.LineHeight = ParseXmlAttribute<double>(xmlElement, "LineHeight", double.NaN);

            textBlock.TextAlignment = ParseXmlAttribute<TextAlignment>(xmlElement, "TextAlignment", TextAlignment.Center);
            textBlock.TextTrimming = ParseXmlAttribute<TextTrimming>(xmlElement, "TextTrimming", TextTrimming.None);
            textBlock.TextWrapping = ParseXmlAttribute<TextWrapping>(xmlElement, "TextWrapping", TextWrapping.NoWrap);

            var textDecorations = GetTextDecorationsFromXElement(xmlElement);
            if (textDecorations != null)
                textBlock.TextDecorations = textDecorations;

            ApplyFontFamily(dialog, textBlock, xmlElement);

            object? padding = GetThicknessFromXElement(xmlElement, "Padding");
            if (padding != null)
                textBlock.Padding = (Thickness)padding;
        }

        private static TextBlock HandleXmlElement_TextBlock(CustomDialog dialog, XElement xmlElement)
        {
            var textBlock = new TextBlock();
            HandleXmlElement_TextBlock_Base(dialog, textBlock, xmlElement);

            textBlock.Text = GetTranslatedText(xmlElement.Attribute("Text")?.Value);

            if (xmlElement.Attribute("Name")?.Value == "StatusText")
            {
                Binding textBinding = new("Message") { Mode = BindingMode.OneWay };
                textBlock.Bind(TextBlock.TextProperty, textBinding);
            }

            return textBlock;
        }

        private static MarkdownTextBlock HandleXmlElement_MarkdownTextBlock(CustomDialog dialog, XElement xmlElement)
        {
            var textBlock = new MarkdownTextBlock();
            HandleXmlElement_TextBlock_Base(dialog, textBlock, xmlElement);

            string? text = GetTranslatedText(xmlElement.Attribute("Text")?.Value);
            if (text != null)
                textBlock.MarkdownText = text;

            return textBlock;
        }

        private static Image HandleXmlElement_Image(CustomDialog dialog, XElement xmlElement)
        {
            var image = new Image();
            HandleXmlElement_FrameworkElement(dialog, image, xmlElement);

            image.Stretch = ParseXmlAttribute<Stretch>(xmlElement, "Stretch", Stretch.Uniform);
            image.StretchDirection = ParseXmlAttribute<StretchDirection>(xmlElement, "StretchDirection", StretchDirection.Both);

            var interpolationMode = ParseXmlAttribute<BitmapInterpolationMode>(xmlElement, "BitmapInterpolationMode", BitmapInterpolationMode.HighQuality);
            RenderOptions.SetBitmapInterpolationMode(image, interpolationMode);

            var imageData = GetImageSourceData(dialog, "Source", xmlElement);

            if (imageData.IsIcon)
            {
                image.Bind(Image.SourceProperty, new Binding("Icon"));
            }
            else if (imageData.Path != null)
            {
                bool isAnimated = ParseXmlAttribute<bool>(xmlElement, "IsAnimated", false);

                if (isAnimated)
                {
                    byte[] bytes = File.ReadAllBytes(imageData.Path);
                    var memoryStream = new MemoryStream(bytes);

                    image.SetValue(ImageBehavior.AnimatedSourceProperty, new AnimatedImageSourceStream(memoryStream));

                    dialog._animatedImages.Add(image);
                }
                else
                {
                    image.Source = new Bitmap(imageData.Path);
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

        private static Border HandleXmlElement_Border(CustomDialog dialog, XElement xmlElement)
        {
            var border = new Avalonia.Controls.Border();

            HandleXmlElement_FrameworkElement(dialog, border, xmlElement);

            object? padding = GetThicknessFromXElement(xmlElement, "Padding");
            if (padding != null) border.Padding = (Thickness)padding;

            object? borderThickness = GetThicknessFromXElement(xmlElement, "BorderThickness");
            if (borderThickness != null) border.BorderThickness = (Thickness)borderThickness;

            object? cornerRadius = GetCornerRadiusFromXElement(xmlElement, "CornerRadius");
            if (cornerRadius != null) border.CornerRadius = (CornerRadius)cornerRadius;

            ApplyBrush_UIElement(dialog, border, "Background", Avalonia.Controls.Border.BackgroundProperty, xmlElement);
            ApplyBrush_UIElement(dialog, border, "BorderBrush", Avalonia.Controls.Border.BorderBrushProperty, xmlElement);

            var childElements = xmlElement.Elements().Where(e => !e.Name.LocalName.Contains('.')).ToList();
            var firstChild = childElements.FirstOrDefault();
            if (firstChild != null)
            {
                border.Child = HandleXml<Control>(dialog, firstChild);
            }

            return border;
        }
        #endregion
    }
}