using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;

namespace Bloxstrap.UI.Elements.Base
{
    public abstract class WpfUiWindow : UiWindow
    {
        private readonly IThemeService _themeService = new ThemeService();
        private Image? _backgroundImageControl;
        private string? _tempGifPath;

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

            Application.Current.Resources["ApplicationBackground"] = null;
            this.Background = null;

            HideBackgroundImageControl();

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
            this.BorderBrush = System.Windows.Media.Brushes.Red;
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

            Application.Current.Resources["ApplicationBackground"] = customBrush;
        }

        private void ApplyImageBackground()
        {
            if (string.IsNullOrEmpty(App.Settings.Prop.BackgroundImagePath) || !File.Exists(App.Settings.Prop.BackgroundImagePath))
            {
                return;
            }

            var extension = Path.GetExtension(App.Settings.Prop.BackgroundImagePath)?.ToLower();

            try
            {
                if (extension == ".gif")
                {
                    ApplyAnimatedGifBackground();
                }
                else
                {
                    ApplyStaticImageBackground();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("WpfUiWindow", $"Exception when changing to image: {ex.Message}");
            }
        }

        private void ApplyStaticImageBackground()
        {
            try
            {
                var imageSource = new BitmapImage();
                imageSource.BeginInit();
                imageSource.CacheOption = BitmapCacheOption.OnLoad;
                imageSource.UriSource = new Uri(App.Settings.Prop.BackgroundImagePath!);
                imageSource.EndInit();
                imageSource.Freeze();

                var imageBrush = new ImageBrush
                {
                    ImageSource = imageSource,
                    Stretch = GetStretchFromSetting(),
                    Opacity = App.Settings.Prop.BackgroundOpacity,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                Application.Current.Resources["ApplicationBackground"] = imageBrush;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("WpfUiWindow", $"Exception when loading static image: {ex.Message}");
            }
        }

        private void ApplyAnimatedGifBackground()
        {
            try
            {
                _backgroundImageControl = GetBackgroundImageControl();

                if (_backgroundImageControl == null)
                {
                    App.Logger.WriteLine("WpfUiWindow", "Failed to get background image control");
                    ApplyStaticImageBackground();
                    return;
                }

                _backgroundImageControl.Stretch = GetStretchFromSetting();
                _backgroundImageControl.Opacity = App.Settings.Prop.BackgroundOpacity;
                _backgroundImageControl.HorizontalAlignment = HorizontalAlignment.Center;
                _backgroundImageControl.VerticalAlignment = VerticalAlignment.Center;

                XamlAnimatedGif.AnimationBehavior.SetSourceUri(_backgroundImageControl, null);

                CleanupTempFile();

                _tempGifPath = Path.Combine(Path.GetTempPath(), $"Froststrap_Background_{Guid.NewGuid()}.gif");
                File.Copy(App.Settings.Prop.BackgroundImagePath!, _tempGifPath, true);

                XamlAnimatedGif.AnimationBehavior.SetSourceUri(_backgroundImageControl, new Uri(_tempGifPath));

                _backgroundImageControl.Visibility = Visibility.Visible;

                Application.Current.Resources["ApplicationBackground"] = null;
                this.Background = null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("WpfUiWindow", $"Exception when loading animated GIF: {ex.Message}");
                ApplyStaticImageBackground();
            }
        }

        private void CleanupTempFile()
        {
            try
            {
                if (!string.IsNullOrEmpty(_tempGifPath) && File.Exists(_tempGifPath))
                {
                    File.Delete(_tempGifPath);
                }
            }
            catch { }
            finally
            {
                _tempGifPath = null;
            }
        }

        private Image? GetBackgroundImageControl()
        {
            var control = this.FindName("BackgroundImageControl") as Image;
            return control;
        }

        private void HideBackgroundImageControl()
        {
            var control = GetBackgroundImageControl();
            if (control != null)
            {
                XamlAnimatedGif.AnimationBehavior.SetSourceUri(control, null);
                control.Visibility = Visibility.Collapsed;
            }

            CleanupTempFile();
        }

        private Stretch GetStretchFromSetting()
        {
            return App.Settings.Prop.BackgroundStretch switch
            {
                BackgroundStretch.None => Stretch.None,
                BackgroundStretch.Fill => Stretch.Fill,
                BackgroundStretch.Uniform => Stretch.Uniform,
                BackgroundStretch.UniformToFill => Stretch.UniformToFill,
                _ => Stretch.UniformToFill
            };
        }

        private void ApplyCustomThemeResources()
        {
            Application.Current.Resources["NewTextEditorBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#59000000"));
            Application.Current.Resources["NewTextEditorForeground"] = new SolidColorBrush(Colors.White);
            Application.Current.Resources["NewTextEditorLink"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A9CEA"));
            Application.Current.Resources["PrimaryBackgroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#19000000"));
            Application.Current.Resources["NormalDarkAndLightBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0FFFFFFF"));
            Application.Current.Resources["ControlFillColorDefault"] = (Color)ColorConverter.ConvertFromString("#19000000");
        }

        private void ApplyStandardTheme(Enums.Theme finalTheme, int customThemeIndex)
        {
            var themeName = Enum.GetName(finalTheme);
            if (themeName != null)
            {
                var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Style/{themeName}.xaml") };
                Application.Current.Resources.MergedDictionaries[customThemeIndex] = dict;
            }

            Application.Current.Resources.Remove("NewTextEditorBackground");
            Application.Current.Resources.Remove("NewTextEditorForeground");
            Application.Current.Resources.Remove("NewTextEditorLink");
            Application.Current.Resources.Remove("PrimaryBackgroundColor");
            Application.Current.Resources.Remove("NormalDarkAndLightBackground");
            Application.Current.Resources.Remove("ControlFillColorDefault");

            HideBackgroundImageControl();
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            HideBackgroundImageControl();
            CleanupTempFile();

            _backgroundImageControl = null;
        }
    }
}