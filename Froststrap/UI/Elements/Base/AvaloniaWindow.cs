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
        private static ResourceDictionary? _activeThemeDictionary;
        private static IStyle? _activeColorStyle;

        public AvaloniaWindow()
        {
            ApplyTheme();
        }

        public static void ApplyTheme()
        {
            var app = Application.Current;
            if (app == null) return;

            var finalTheme = App.Settings.Prop.Theme.GetFinal();
            app.RequestedThemeVariant = finalTheme == Enums.Theme.Light ? ThemeVariant.Light : ThemeVariant.Dark;

            var faTheme = app.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
            if (faTheme != null) faTheme.PreferSystemTheme = false;

            RemoveCurrentThemeAndStyles();

            if (finalTheme == Enums.Theme.Custom)
            {
                if (App.Settings.Prop.BackgroundType == BackgroundMode.Gradient)
                    ApplyGradientBackground();
                else if (App.Settings.Prop.BackgroundType == BackgroundMode.Image)
                    ApplyImageBackground();

                ApplyCustomThemeResources();
            }
            else
            {
                ApplyStandardTheme(finalTheme);
            }

            UpdateAllWindows();
        }

        private static void ApplyStandardTheme(Theme finalTheme)
        {
            var app = Application.Current;
            if (app == null) return;

            string themeName = Enum.GetName(finalTheme) ?? "Dark";

            try
            {
                var themeUri = new Uri($"avares://Froststrap/UI/AppThemes/ResourceDictionarys/{themeName}.axaml");
                if (AvaloniaXamlLoader.Load(themeUri) is ResourceDictionary dict)
                {
                    _activeThemeDictionary = dict;
                    app.Resources.MergedDictionaries.Add(dict);
                }

                var styleUri = new Uri($"avares://Froststrap/UI/AppThemes/Styles/{themeName}.axaml");
                if (AvaloniaXamlLoader.Load(styleUri) is Styles loadedStyles)
                {
                    _activeColorStyle = loadedStyles;
                    app.Styles.Insert(1, loadedStyles);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AvaloniaWindow", $"Error loading {themeName}: {ex.Message}");
            }
        }

        private static void RemoveCurrentThemeAndStyles()
        {
            var app = Application.Current;
            if (app == null) return;

            if (_activeThemeDictionary != null)
            {
                app.Resources.MergedDictionaries.Remove(_activeThemeDictionary);
                _activeThemeDictionary = null;
            }

            if (_activeColorStyle != null)
            {
                app.Styles.Remove(_activeColorStyle);
                _activeColorStyle = null;
            }

            app.Resources.Remove("ApplicationBackground");
        }

        private static void ApplyGradientBackground()
        {
            double angleRad = App.Settings.Prop.GradientAngle * Math.PI / 180.0;
            var customBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5 + 0.5 * Math.Cos(angleRad + Math.PI), 0.5 + 0.5 * Math.Sin(angleRad + Math.PI), RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5 + 0.5 * Math.Cos(angleRad), 0.5 + 0.5 * Math.Sin(angleRad), RelativeUnit.Relative)
            };

            foreach (var stop in (App.Settings.Prop.CustomGradientStops ?? new List<Models.GradientStops>()).OrderBy(s => s.Offset))
            {
                try { customBrush.GradientStops.Add(new GradientStop(Color.Parse(stop.Color), stop.Offset)); } catch { }
            }

            SetResource("ApplicationBackground", customBrush);
        }

        private static void ApplyImageBackground()
        {
            string? path = App.Settings.Prop.BackgroundImagePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                using var stream = File.OpenRead(path);
                SetResource("ApplicationBackground", new ImageBrush
                {
                    Source = new Bitmap(stream),
                    Stretch = (Stretch)App.Settings.Prop.BackgroundStretch,
                    Opacity = App.Settings.Prop.BackgroundOpacity
                });
            }
            catch (Exception ex) { App.Logger.WriteLine("AvaloniaWindow", ex.Message); }
        }

        private static void ApplyCustomThemeResources()
        {
            SetResource("NewTextEditorBackground", new SolidColorBrush(Color.Parse("#59000000")));
            SetResource("NewTextEditorForeground", Brushes.White);
            SetResource("NewTextEditorLink", new SolidColorBrush(Color.Parse("#3A9CEA")));
            SetResource("PrimaryBackgroundColor", new SolidColorBrush(Color.Parse("#19000000")));
            SetResource("NormalDarkAndLightBackground", new SolidColorBrush(Color.Parse("#0FFFFFFF")));
            SetResource("ControlFillColorDefault", Color.Parse("#19000000"));
        }

        private static void SetResource(string key, object value) => Application.Current!.Resources[key] = value;

        public static void UpdateAllWindows()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows.OfType<AvaloniaWindow>()) window.UpdateWindowBackground();
            }
        }

        public void UpdateWindowBackground()
        {
            if (Application.Current?.Resources.TryGetResource("ApplicationBackground", null, out var background) == true)
                this.Background = background as IBrush;
            else
                this.Background = null;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            UpdateWindowBackground();
        }
    }
}