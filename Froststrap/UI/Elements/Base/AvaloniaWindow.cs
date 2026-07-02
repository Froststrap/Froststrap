using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using FluentAvalonia.Styling;

namespace Froststrap.UI.Elements.Base
{
    public abstract class AvaloniaWindow : Window
    {
        private static IStyle? _activeColorStyle;
        private static ResourceDictionary? _activeThemeDictionary;

        public AvaloniaWindow()
        {
            this.WindowDecorations = OperatingSystem.IsMacOS() ? WindowDecorations.Full : WindowDecorations.None;
            this.ExtendClientAreaToDecorationsHint = true;
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

            if (finalTheme != Enums.Theme.Custom)
            {
                try
                {
                    Application.Current.Resources.Remove("ApplicationBackgroundColor");

                    var themeUri = new Uri($"avares://Froststrap/UI/AppThemes/ResourceDictionarys/{themeName}.axaml");
                    var loadedTheme = AvaloniaXamlLoader.Load(themeUri);
                    if (loadedTheme is ResourceDictionary dict)
                    {
                        _activeThemeDictionary = dict;
                        Application.Current.Resources.MergedDictionaries.Add(dict);
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
                IBrush? customBackground = null;

                if (App.Settings.Prop.BackgroundType == BackgroundMode.Gradient)
                {
                    var avaloniaStops = new Avalonia.Media.GradientStops();

                    foreach (var s in App.Settings.Prop.CustomGradientStops)
                    {
                        if (Color.TryParse(s.Color, out var color))
                        {
                            avaloniaStops.Add(new GradientStop(color, s.Offset));
                        }
                    }

                    double angleRad = (Math.PI / 180.0) * (App.Settings.Prop.GradientAngle - 90);
                    var startPoint = new RelativePoint(
                        0.5 - Math.Cos(angleRad) * 0.5,
                        0.5 - Math.Sin(angleRad) * 0.5,
                        RelativeUnit.Relative);
                    var endPoint = new RelativePoint(
                        0.5 + Math.Cos(angleRad) * 0.5,
                        0.5 + Math.Sin(angleRad) * 0.5,
                        RelativeUnit.Relative);

                    customBackground = new LinearGradientBrush
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
                            var bitmap = new Bitmap(path);
                            customBackground = new ImageBrush(bitmap)
                            {
                                Stretch = (Stretch)App.Settings.Prop.BackgroundStretch,
                                Opacity = App.Settings.Prop.BackgroundOpacity
                            };
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine("AvaloniaWindow", $"Image load error: {ex.Message}");
                        }
                    }
                }

                if (customBackground != null)
                {
                    Application.Current.Resources["ApplicationBackgroundColor"] = customBackground;
                }
                else
                {
                    Application.Current.Resources["ApplicationBackgroundColor"] = Brushes.Transparent;
                }
            }

            UpdateBackdropForAllWindows();
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

            UpdateBackdropForAllWindows();
            Locale.ApplyLocaleToWindow(this);
        }
    }
}