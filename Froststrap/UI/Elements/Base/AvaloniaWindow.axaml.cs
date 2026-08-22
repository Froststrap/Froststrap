using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using Froststrap.AttachedProperties;

namespace Froststrap.UI.Elements.Base
{
    public abstract class AvaloniaWindow : Window
    {
        private static IStyle? _activeColorStyle;
        private static ResourceDictionary? _activeThemeDictionary;
        private static IBrush? _currentBackgroundBrush;
        private static Bitmap? _currentBackgroundBitmap;
        private static string? _currentBitmapPath;
        private static readonly string[] AnimatedImageExtensions = [".gif"];

        protected virtual bool ApplyTopPadding => true;

        public AvaloniaWindow()
        {
            WindowDecorations = WindowDecorations.Full;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = -1;
            MacOSTitleBar.SetIsThick(this, true);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);

            if (ApplyTopPadding && OperatingSystem.IsWindows())
                Padding = new Thickness(0, 32, 0, 0);

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
            bool isAnimatedGif = false;
            string? backgroundImagePath = null;
            Bitmap? backgroundBitmap = null;
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
                    App.Logger.Error($"Theme/Style loading error for {themeName}: {ex.Message}");
                }
            }
            else
            {
                var customDict = new ResourceDictionary
                {
                    ["NotificationBackgroundColor"] = new SolidColorBrush(Color.Parse("#2D2D2D"))
                };

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
                            App.Logger.Error($"Image load error: {ex.Message}");
                        }
                    }
                    else
                    {
                        _currentBitmapPath = null;
                    }
                }
            }

            _currentBackgroundBrush = backgroundBrush ?? Brushes.Transparent;

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
            this.Background = _currentBackgroundBrush ?? Brushes.Transparent;
        }

        //TODO: Fix none applying blur transparency even after app restart
        public static void UpdateBackdropForAllWindows()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            var selectedBackdrop = App.Settings.Prop.SelectedBackdrop;

            foreach (var window in desktop.Windows)
            {
                if (OperatingSystem.IsWindows() && selectedBackdrop != WindowsBackdrops.None)
                {
                    window.TransparencyLevelHint = selectedBackdrop switch
                    {
                        WindowsBackdrops.Acrylic => [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None],
                        WindowsBackdrops.Mica => [WindowTransparencyLevel.Mica, WindowTransparencyLevel.None],
                        WindowsBackdrops.Aero => [WindowTransparencyLevel.Blur, WindowTransparencyLevel.None],
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
            ApplyWindowBackground();
            UpdateBackdropForAllWindows();
            Locale.ApplyLocaleToWindow(this);
        }
    }
}