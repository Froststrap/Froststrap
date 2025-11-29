using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;

namespace Bloxstrap.UI.Elements.Base
{
    public abstract class WpfUiWindow : UiWindow
    {
        private readonly IThemeService _themeService = new ThemeService();

        public WpfUiWindow()
        {
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            const int customThemeIndex = 2;

            var finalTheme = App.Settings.Prop.Theme.GetFinal();

            _themeService.SetTheme(finalTheme == Enums.Theme.Light ? ThemeType.Light : ThemeType.Dark);
            _themeService.SetSystemAccent();

            if (finalTheme == Enums.Theme.Custom)
            {
                Application.Current.Resources["WindowBackgroundGradient"] = null;

                double angle = App.Settings.Prop.GradientAngle;
                double angleRad = angle * Math.PI / 180.0;

                double startX = 0.5 + 0.5 * Math.Cos(angleRad + Math.PI);
                double startY = 0.5 + 0.5 * Math.Sin(angleRad + Math.PI);
                double endX = 0.5 + 0.5 * Math.Cos(angleRad);
                double endY = 0.5 + 0.5 * Math.Sin(angleRad);

                var customBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(startX, startY),
                    EndPoint = new Point(endX, endY)
                };

                foreach (var stop in App.Settings.Prop.CustomGradientStops.OrderBy(s => s.Offset))
                {
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(stop.Color);
                        customBrush.GradientStops.Add(new GradientStop(color, stop.Offset));
                    }
                    catch { }
                }

                Application.Current.Resources["WindowBackgroundGradient"] = customBrush;

                Application.Current.Resources["NewTextEditorBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#59000000"));
                Application.Current.Resources["NewTextEditorForeground"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["NewTextEditorLink"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A9CEA"));
                Application.Current.Resources["PrimaryBackgroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#19000000"));
                Application.Current.Resources["NormalDarkAndLightBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0FFFFFFF"));
                Application.Current.Resources["ControlFillColorDefault"] = (Color)ColorConverter.ConvertFromString("#19000000");
            }
            else
            {
                var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Style/{Enum.GetName(finalTheme)}.xaml") };
                Application.Current.Resources.MergedDictionaries[customThemeIndex] = dict;

                Application.Current.Resources["WindowBackgroundGradient"] = null;
                Application.Current.Resources.Remove("NewTextEditorBackground");
                Application.Current.Resources.Remove("NewTextEditorForeground");
                Application.Current.Resources.Remove("NewTextEditorLink");
                Application.Current.Resources.Remove("PrimaryBackgroundColor");
                Application.Current.Resources.Remove("NormalDarkAndLightBackground");
                Application.Current.Resources.Remove("ControlFillColorDefault");
            }

#if QA_BUILD
    this.BorderBrush = System.Windows.Media.Brushes.Red;
    this.BorderThickness = new Thickness(4);
#endif
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Hardware Accel
            if (App.Settings.Prop.WPFSoftwareRender || App.LaunchSettings.NoGPUFlag.Active)
            {
                if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            // Custom Font
            string? fontPath = App.Settings.Prop.CustomFontPath;
            if (!string.IsNullOrWhiteSpace(fontPath) && File.Exists(fontPath))
            {
                var font = FontManager.LoadFontFromFile(fontPath);
                if (font != null)
                {
                    this.FontFamily = font;
                }
            }
        }
    }
}