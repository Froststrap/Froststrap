using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace Froststrap.UI.Elements.Base
{
    public abstract class AvaloniaWindow : Window
    {
        private static ResourceDictionary? _currentTheme;

        public AvaloniaWindow()
        {
            ApplyGlobalTheme();
        }

        public static void ApplyGlobalTheme()
        {
            var finalTheme = App.Settings.Prop.Theme.GetFinal();

            Application.Current!.RequestedThemeVariant = finalTheme == Enums.Theme.Light ?
                ThemeVariant.Light : ThemeVariant.Dark;

            Application.Current.Resources.Remove("ApplicationBackground");

            if (finalTheme == Enums.Theme.Custom)
            {
                if (App.Settings.Prop.BackgroundType == BackgroundMode.Gradient)
                {
                    ApplyGradientBackground();
                }
                else if (App.Settings.Prop.BackgroundType == BackgroundMode.Image)
                {
                    ApplyImageBackground();
                }

                ApplyCustomThemeResources();
            }
            else
            {
                ApplyStandardTheme(finalTheme);
            }

            UpdateAllWindows();
        }

        private static void ApplyGradientBackground()
        {
            double angle = App.Settings.Prop.GradientAngle;
            double angleRad = angle * Math.PI / 180.0;

            double startX = 0.5 + 0.5 * Math.Cos(angleRad + Math.PI);
            double startY = 0.5 + 0.5 * Math.Sin(angleRad + Math.PI);
            double endX = 0.5 + 0.5 * Math.Cos(angleRad);
            double endY = 0.5 + 0.5 * Math.Sin(angleRad);

            var customBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(startX, startY, RelativeUnit.Relative),
                EndPoint = new RelativePoint(endX, endY, RelativeUnit.Relative)
            };

            customBrush.GradientStops.Clear();

            var currentStops = App.Settings.Prop.CustomGradientStops ?? new List<Models.GradientStops>();

            foreach (var stop in currentStops.OrderBy(s => s.Offset))
            {
                try
                {
                    var color = ParseColor(stop.Color);
                    customBrush.GradientStops.Add(new GradientStop(color, stop.Offset));
                }
                catch { }
            }

            SetResource("ApplicationBackground", customBrush);
        }

        private static void ApplyImageBackground()
        {
            if (string.IsNullOrEmpty(App.Settings.Prop.BackgroundImagePath) ||
                !File.Exists(App.Settings.Prop.BackgroundImagePath))
            {
                return;
            }

            try
            {
                using var stream = File.OpenRead(App.Settings.Prop.BackgroundImagePath);
                var imageSource = new Bitmap(stream);

                var imageBrush = new ImageBrush
                {
                    Source = imageSource,
                    Stretch = App.Settings.Prop.BackgroundStretch switch
                    {
                        BackgroundStretch.None => Stretch.None,
                        BackgroundStretch.Fill => Stretch.Fill,
                        BackgroundStretch.Uniform => Stretch.Uniform,
                        BackgroundStretch.UniformToFill => Stretch.UniformToFill,
                        _ => Stretch.UniformToFill
                    },
                    Opacity = App.Settings.Prop.BackgroundOpacity
                };

                SetResource("ApplicationBackground", imageBrush);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AvaloniaWindow", $"Exception when changing to image: {ex.Message}");
            }
        }

        private static Color ParseColor(string colorString)
        {
            return Color.Parse(colorString);
        }

        private static void ApplyCustomThemeResources()
        {
            RemoveCurrentTheme();

            SetResource("NewTextEditorBackground", new SolidColorBrush(ParseColor("#59000000")));
            SetResource("NewTextEditorForeground", new SolidColorBrush(Colors.White));
            SetResource("NewTextEditorLink", new SolidColorBrush(ParseColor("#3A9CEA")));
            SetResource("PrimaryBackgroundColor", new SolidColorBrush(ParseColor("#19000000")));
            SetResource("NormalDarkAndLightBackground", new SolidColorBrush(ParseColor("#0FFFFFFF")));
            SetResource("ControlFillColorDefault", ParseColor("#19000000"));
        }

        public static void RefreshCustomTheme()
        {
            if (App.Settings.Prop.Theme == Enums.Theme.Custom)
            {
                ApplyGradientBackground();
                UpdateAllWindows();
            }
        }

        private static void ApplyStandardTheme(Theme finalTheme)
        {
            try
            {
                var themeName = Enum.GetName(finalTheme);
                if (themeName == null) return;

                RemoveCurrentTheme();

                RemoveCustomResources();

                var uri = new Uri($"avares://Froststrap/UI/Style/{themeName}.axaml");
                LoadThemeFromUri(uri);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AvaloniaWindow", $"Error loading theme: {ex.Message}");
            }
        }

        private static void LoadThemeFromUri(Uri uri)
        {
            try
            {
                var resources = Application.Current?.Resources;
                if (resources == null) return;

                var themeDict = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);

                MergeResourceDictionary(resources, themeDict);

                _currentTheme = themeDict;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AvaloniaWindow", $"Error loading theme from URI: {ex.Message}");
            }
        }

        private static void MergeResourceDictionary(IResourceDictionary target, ResourceDictionary source)
        {
            foreach (var key in source.Keys)
            {
                if (source.TryGetResource(key, null, out var value))
                {
                    target[key] = value;
                }
            }
        }

        private static void RemoveCurrentTheme()
        {
            if (_currentTheme != null)
            {
                var resources = Application.Current?.Resources;
                if (resources != null)
                {
                    foreach (var key in _currentTheme.Keys)
                    {
                        resources.Remove(key);
                    }
                }
                _currentTheme = null;
            }
        }

        private static void RemoveCustomResources()
        {
            var resources = Application.Current?.Resources;
            if (resources != null)
            {
                resources.Remove("NewTextEditorBackground");
                resources.Remove("NewTextEditorForeground");
                resources.Remove("NewTextEditorLink");
                resources.Remove("PrimaryBackgroundColor");
                resources.Remove("NormalDarkAndLightBackground");
                resources.Remove("ControlFillColorDefault");
            }
        }

        private static void SetResource(string key, object value)
        {
            var resources = Application.Current?.Resources;
            if (resources != null)
            {
                resources[key] = value;
            }
        }

        private static void UpdateAllWindows()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    if (window is AvaloniaWindow avaloniaWindow)
                    {
                        avaloniaWindow.UpdateWindowBackground();
                    }
                }
            }
        }

        private void UpdateWindowBackground()
        {
            if (Application.Current?.Resources.TryGetResource("ApplicationBackground", null, out var background) == true)
            {
                this.Background = background as IBrush;
            }
            else
            {
                this.Background = null;
            }
        }
    }
}