using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using SukiUI;
using SukiUI.Controls;
using SukiUI.Enums;

namespace Froststrap.UI.Elements.Base
{
    public abstract class AvaloniaWindow : SukiWindow
    {
        private static IStyle? _activeColorStyle;
        private static ResourceDictionary? _activeThemeDictionary;

        public AvaloniaWindow()
        {
            // Remove suki titlebar + Change background Style
            this.IsTitleBarVisible = false;
            this.ShowBottomBorder = false;
            this.BackgroundStyle = SukiBackgroundStyle.GradientSoft;

            // This is so we can resize the window
            this.ExtendClientAreaToDecorationsHint = true;
            this.ExtendClientAreaTitleBarHeightHint = -1;
            this.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            this.SystemDecorations = SystemDecorations.Full;

            ApplyTheme();
        }

        public static void ApplyTheme()
        {
            if (Application.Current == null) return;

            var finalTheme = App.Settings.Prop.Theme.GetFinal();
            string themeName = Enum.GetName(finalTheme) ?? "Dark";

            var sukiTheme = SukiTheme.GetInstance();

            sukiTheme.ChangeColorTheme(SukiColor.Blue);

            Application.Current.RequestedThemeVariant = finalTheme == Enums.Theme.Light
                ? ThemeVariant.Light
                : ThemeVariant.Dark;

            if (finalTheme == Enums.Theme.Light)
                sukiTheme.ChangeBaseTheme(ThemeVariant.Light);
            else
                sukiTheme.ChangeBaseTheme(ThemeVariant.Dark);

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
                    if (AvaloniaXamlLoader.Load(themeUri) is ResourceDictionary dict)
                    {
                        _activeThemeDictionary = dict;
                        Application.Current.Resources.MergedDictionaries.Add(dict);
                    }

                    var styleUri = new Uri($"avares://Froststrap/UI/AppThemes/Styles/{themeName}.axaml");
                    if (AvaloniaXamlLoader.Load(styleUri) is Styles loadedStyles)
                    {
                        _activeColorStyle = (IStyle)loadedStyles;
                        Application.Current.Styles.Insert(1, (IStyle)loadedStyles);
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
                            avaloniaStops.Add(new GradientStop(color, s.Offset));
                    }

                    double angleRad = (Math.PI / 180.0) * (App.Settings.Prop.GradientAngle - 90);
                    var startPoint = new RelativePoint(0.5 - Math.Cos(angleRad) * 0.5, 0.5 - Math.Sin(angleRad) * 0.5, RelativeUnit.Relative);
                    var endPoint = new RelativePoint(0.5 + Math.Cos(angleRad) * 0.5, 0.5 + Math.Sin(angleRad) * 0.5, RelativeUnit.Relative);

                    customBackground = new LinearGradientBrush { GradientStops = avaloniaStops, StartPoint = startPoint, EndPoint = endPoint };
                }
                else if (App.Settings.Prop.BackgroundType == BackgroundMode.Image)
                {
                    string path = App.Settings.Prop.BackgroundImagePath ?? string.Empty;
                    if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                    {
                        try
                        {
                            customBackground = new ImageBrush(new Bitmap(path))
                            {
                                Stretch = (Stretch)App.Settings.Prop.BackgroundStretch,
                                Opacity = App.Settings.Prop.BackgroundOpacity
                            };
                        }
                        catch (Exception ex) { App.Logger.WriteLine("AvaloniaWindow", $"Image load error: {ex.Message}"); }
                    }
                }

                Application.Current.Resources["ApplicationBackgroundColor"] = customBackground ?? Brushes.Transparent;
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
#if QA_BUILD
            this.BorderBrush = Brushes.Red;
            this.BorderThickness = new Thickness(4);
#endif
        }
    }
}