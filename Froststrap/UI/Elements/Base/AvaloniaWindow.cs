using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace Froststrap.UI.Elements.Base
{
    public abstract class AvaloniaWindow : Window
    {
        private readonly IStyleHost _styleHost;

        public AvaloniaWindow()
        {
            _styleHost = Application.Current as IStyleHost ?? this;
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            const int customThemeIndex = 2;

            var finalTheme = App.Settings.Prop.Theme.GetFinal();

            RequestedThemeVariant = finalTheme == Enums.Theme.Light ?
                ThemeVariant.Light : ThemeVariant.Dark;

            if (_styleHost.Styles.Count > customThemeIndex)
            {
                _styleHost.Styles.RemoveAt(customThemeIndex);
            }

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
                ApplyStandardTheme(finalTheme, customThemeIndex);
            }

#if QA_BUILD
            this.BorderBrush = Avalonia.Media.Brushes.Red;
            this.BorderThickness = new Thickness(4);
#endif
        }

        private void ApplyGradientBackground()
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

            foreach (var stop in App.Settings.Prop.CustomGradientStops.OrderBy(s => s.Offset))
            {
                try
                {
                    var color = ParseColor(stop.Color);
                    customBrush.GradientStops.Add(new GradientStop(color, stop.Offset));
                }
                catch { }
            }

            Application.Current?.Resources["ApplicationBackground"] = customBrush;

            this.Background = customBrush;
        }

        private void ApplyImageBackground()
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

                Application.Current?.Resources["ApplicationBackground"] = imageBrush;

                this.Background = imageBrush;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AvaloniaWindow", $"Exception when changing to image: {ex.Message}");
            }
        }

        private Color ParseColor(string colorString)
        {
            if (colorString.StartsWith("#"))
            {
                return Color.Parse(colorString);
            }

            return Color.Parse(colorString);
        }

        private void ApplyCustomThemeResources()
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            resources["NewTextEditorBackground"] = new SolidColorBrush(ParseColor("#59000000"));
            resources["NewTextEditorForeground"] = new SolidColorBrush(Colors.White);
            resources["NewTextEditorLink"] = new SolidColorBrush(ParseColor("#3A9CEA"));
            resources["PrimaryBackgroundColor"] = new SolidColorBrush(ParseColor("#19000000"));
            resources["NormalDarkAndLightBackground"] = new SolidColorBrush(ParseColor("#0FFFFFFF"));
            resources["ControlFillColorDefault"] = ParseColor("#19000000");
        }

        private void ApplyStandardTheme(Theme finalTheme, int customThemeIndex)
        {
            try
            {
                var themeName = Enum.GetName(finalTheme);
                if (themeName == null) return;

                var uri = new Uri($"avares://Froststrap/UI/Style/{themeName}.axaml");

                var style = new StyleInclude(uri)
                {
                    Source = uri
                };

                if (_styleHost.Styles.Count > customThemeIndex)
                {
                    _styleHost.Styles[customThemeIndex] = style;
                }
                else
                {
                    _styleHost.Styles.Add(style);
                }

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
            catch (Exception ex)
            {
                App.Logger.WriteLine("AvaloniaWindow", $"Error loading theme: {ex.Message}");
            }
        }
    }
}