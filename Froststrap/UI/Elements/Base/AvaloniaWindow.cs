using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AnimatedImage.Avalonia;
using FluentAvalonia.Styling;

namespace Froststrap.UI.Elements.Base
{
    public abstract class AvaloniaWindow : Window
    {
        private static IStyle? _activeColorStyle;
        private static ResourceDictionary? _activeThemeDictionary;

        private static IBrush? _currentBackgroundBrush;
        private static Bitmap? _currentBackgroundBitmap;
        private static string? _currentBitmapPath;
        private static string? _currentAnimatedImagePath;
        private static bool _currentIsAnimatedGif;
        private static Stretch _currentImageStretch = Stretch.UniformToFill;
        private static double _currentImageOpacity = 1.0;

        private static readonly string[] AnimatedImageExtensions = [".gif"];

        static AvaloniaWindow()
        {
            ContentProperty.OverrideMetadata<AvaloniaWindow>(
                new StyledPropertyMetadata<object?>(coerce: CoerceContent));
        }

        private static object? CoerceContent(AvaloniaObject sender, object? value)
        {
            if (sender is not AvaloniaWindow window || window._contentHost is null)
                return value;

            if (ReferenceEquals(value, window._backgroundHost))
                return value;

            window._contentHost.Content = value;
            return window._backgroundHost;
        }

        private readonly Panel _backgroundHost;
        private readonly Image _backgroundImage;
        private readonly ContentControl _contentHost;

        public AvaloniaWindow()
        {
            if (!OperatingSystem.IsMacOS())
            {
                this.WindowDecorations = WindowDecorations.BorderOnly;
                this.ExtendClientAreaToDecorationsHint = true;
            }
            else
            {
                this.WindowDecorations = WindowDecorations.Full;
            }

            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);

            _backgroundImage = new Image
            {
                IsVisible = false,
                IsHitTestVisible = false,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            RenderOptions.SetBitmapInterpolationMode(_backgroundImage, BitmapInterpolationMode.HighQuality);

            _contentHost = new ContentControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };

            _backgroundHost = new Panel();
            _backgroundHost.Children.Add(_backgroundImage);
            _backgroundHost.Children.Add(_contentHost);

            ApplyTheme();
        }

        public static void ApplyTheme()
        {
            if (Application.Current == null) return;

            var finalTheme = App.Settings.Prop.Theme.GetFinal();
            string themeName = Enum.GetName(finalTheme) ?? "Dark";

            Application.Current.RequestedThemeVariant = finalTheme == Enums.Theme.Light
                ? ThemeVariant.Light
                : ThemeVariant.Dark;

            var faTheme = Application.Current.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
            if (faTheme != null)
            {
                faTheme.PreferSystemTheme = false;
                faTheme.PreferUserAccentColor = true;
            }

            if (_activeThemeDictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(_activeThemeDictionary);
                _activeThemeDictionary = null;
            }

            if (_activeColorStyle != null)
            {
                Application.Current.Styles.Remove(_activeColorStyle);
                _activeColorStyle = null;
            }

            IBrush? backgroundBrush = null;
            Bitmap? backgroundBitmap = null;
            string? backgroundImagePath = null;
            bool isAnimatedGif = false;
            Stretch imageStretch = Stretch.UniformToFill;
            double imageOpacity = 1.0;

            if (finalTheme != Enums.Theme.Custom)
            {
                try
                {
                    var themeUri = new Uri($"avares://Froststrap/UI/AppThemes/ResourceDictionarys/{themeName}.axaml");
                    var loadedTheme = AvaloniaXamlLoader.Load(themeUri);
                    if (loadedTheme is ResourceDictionary dict)
                    {
                        _activeThemeDictionary = dict;
                        Application.Current.Resources.MergedDictionaries.Add(dict);

                        if (dict.TryGetValue("ApplicationBackgroundColor", out var themeBg))
                            backgroundBrush = themeBg as IBrush;
                    }

                    var styleUri = new Uri($"avares://Froststrap/UI/AppThemes/Styles/{themeName}.axaml");
                    var loadedStyle = AvaloniaXamlLoader.Load(styleUri);
                    if (loadedStyle is Styles loadedStyles)
                    {
                        _activeColorStyle = loadedStyles;
                        Application.Current.Styles.Insert(1, loadedStyles);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("AvaloniaWindow", $"Theme/Style loading error for {themeName}: {ex.Message}");
                }
            }
            else
            {
                var customDict = new ResourceDictionary();
                customDict["NotificationBackgroundColor"] = new SolidColorBrush(Color.Parse("#2D2D2D"));
                _activeThemeDictionary = customDict;
                Application.Current.Resources.MergedDictionaries.Add(customDict);

                if (App.Settings.Prop.BackgroundType == BackgroundMode.Gradient)
                {
                    var avaloniaStops = new Avalonia.Media.GradientStops();

                    foreach (var s in App.Settings.Prop.CustomGradientStops)
                    {
                        if (Color.TryParse(s.Color, out var color))
                            avaloniaStops.Add(new Avalonia.Media.GradientStop(color, s.Offset));
                    }

                    double angle = App.Settings.Prop.GradientAngle ?? 45;
                    double angleRad = (Math.PI / 180.0) * (angle - 90);

                    var startPoint = new RelativePoint(
                        0.5 - Math.Cos(angleRad) * 0.5,
                        0.5 - Math.Sin(angleRad) * 0.5,
                        RelativeUnit.Relative);
                    var endPoint = new RelativePoint(
                        0.5 + Math.Cos(angleRad) * 0.5,
                        0.5 + Math.Sin(angleRad) * 0.5,
                        RelativeUnit.Relative);

                    backgroundBrush = new LinearGradientBrush
                    {
                        GradientStops = avaloniaStops,
                        StartPoint = startPoint,
                        EndPoint = endPoint
                    };
                }
                else if (App.Settings.Prop.BackgroundType == BackgroundMode.Image)
                {
                    string path = App.Settings.Prop.BackgroundImagePath ?? string.Empty;

                    if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                    {
                        try
                        {
                            string extension = System.IO.Path.GetExtension(path);
                            isAnimatedGif = AnimatedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

                            if (isAnimatedGif)
                            {
                                backgroundImagePath = path;
                            }
                            else
                            {
                                backgroundBitmap = (path == _currentBitmapPath && _currentBackgroundBitmap != null)
                                    ? _currentBackgroundBitmap
                                    : new Bitmap(path);
                                _currentBitmapPath = path;
                            }

                            imageStretch = (Stretch)App.Settings.Prop.BackgroundStretch;
                            imageOpacity = App.Settings.Prop.BackgroundOpacity;
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine("AvaloniaWindow", $"Image load error: {ex.Message}");
                        }
                    }
                    else
                    {
                        _currentBitmapPath = null;
                    }
                }
            }

            _currentBackgroundBrush = backgroundBrush ?? Brushes.Transparent;
            _currentBackgroundBitmap = backgroundBitmap;
            _currentAnimatedImagePath = backgroundImagePath;
            _currentIsAnimatedGif = isAnimatedGif;
            _currentImageStretch = imageStretch;
            _currentImageOpacity = imageOpacity;

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    if (window is AvaloniaWindow avaloniaWindow)
                        avaloniaWindow.ApplyWindowBackground();
                }
            }

            UpdateBackdropForAllWindows();
        }

        private void ApplyWindowBackground()
        {
            bool showImage = _currentIsAnimatedGif || _currentBackgroundBitmap != null;

            _backgroundHost.Background = showImage ? Brushes.Transparent : _currentBackgroundBrush;

            _backgroundImage.IsVisible = showImage;
            _backgroundImage.Stretch = _currentImageStretch;
            _backgroundImage.Opacity = _currentImageOpacity;

            if (_currentIsAnimatedGif && _currentAnimatedImagePath is not null)
            {
                var uri = new Uri(_currentAnimatedImagePath, UriKind.Absolute);
                ImageBehavior.SetAnimatedSource(_backgroundImage, new AnimatedImageSourceUri(uri));
                ImageBehavior.SetRepeatBehavior(_backgroundImage, RepeatBehavior.Forever);
            }
            else
            {
                if (_backgroundImage.GetValue(ImageBehavior.AnimatedSourceProperty) is not null)
                    ImageBehavior.SetAnimatedSource(_backgroundImage, null!);

                _backgroundImage.Source = _currentBackgroundBitmap;
            }
        }

        public static void UpdateBackdropForAllWindows()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            var selectedBackdrop = App.Settings.Prop.SelectedBackdrop;

            foreach (var window in desktop.Windows)
            {
                if (selectedBackdrop != Enums.WindowsBackdrops.None)
                {
                    window.TransparencyLevelHint = selectedBackdrop switch
                    {
                        Enums.WindowsBackdrops.Acrylic => [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None],
                        Enums.WindowsBackdrops.Mica => [WindowTransparencyLevel.Mica, WindowTransparencyLevel.None],
                        Enums.WindowsBackdrops.Aero => [WindowTransparencyLevel.Blur, WindowTransparencyLevel.None],
                        _ => [WindowTransparencyLevel.None]
                    };
                    window.Background = Brushes.Transparent;
                }
                else
                {
                    window.TransparencyLevelHint = [WindowTransparencyLevel.None];
                }
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
#if QA_BUILD            
            this.BorderBrush = Brushes.Red;
            this.BorderThickness = new Thickness(4);
#endif

            ApplyWindowBackground();
            UpdateBackdropForAllWindows();
            Locale.ApplyLocaleToWindow(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_backgroundImage.GetValue(ImageBehavior.AnimatedSourceProperty) is not null)
                ImageBehavior.SetAnimatedSource(_backgroundImage, null!);
        }
    }
}