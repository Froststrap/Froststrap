using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.Styling;

namespace Froststrap.UI.Elements.Base
{
    public abstract class AvaloniaWindow : Window
    {
        private static IStyle? _activeColorStyle;
        private static ResourceDictionary? _activeThemeDictionary;

        private static IBrush? _currentBackgroundBrush;

        public AvaloniaWindow()
        {
            WindowDecorations = WindowDecorations.Full;
            ExtendClientAreaToDecorationsHint = false;

            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
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
            // avoid custom background image host/content wrapping.
            // Only apply brush background.
            this.Background = _currentBackgroundBrush ?? Brushes.Transparent;
        }

        public static void UpdateBackdropForAllWindows()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            bool isWindows = OperatingSystem.IsWindows();
            var selectedBackdrop = App.Settings.Prop.SelectedBackdrop;

            foreach (var window in desktop.Windows)
            {
                if (isWindows && selectedBackdrop != Enums.WindowsBackdrops.None)
                {
                    window.TransparencyLevelHint = selectedBackdrop switch
                    {
                        Enums.WindowsBackdrops.Mica    => new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None },
                        Enums.WindowsBackdrops.Acrylic => new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None },
                        Enums.WindowsBackdrops.Aero    => new[] { WindowTransparencyLevel.Blur, WindowTransparencyLevel.None },
                        _                              => new[] { WindowTransparencyLevel.None }
                    };

                    window.Background = Brushes.Transparent;
                }
                else
                {
                    // Linux/macOS: force opaque for stability
                    window.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
                    window.Opacity = 1.0;
                    window.Background = _currentBackgroundBrush ?? new SolidColorBrush(Color.Parse("#202020"));
                }
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            WindowDecorations = WindowDecorations.Full;
            ExtendClientAreaToDecorationsHint = false;

            ApplyWindowBackground();
            UpdateBackdropForAllWindows();
            Locale.ApplyLocaleToWindow(this);
        }
    }
}
