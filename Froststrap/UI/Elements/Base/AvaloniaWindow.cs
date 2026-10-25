// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using AnimatedImage.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using Froststrap.UI.Utility;
using System.Runtime.CompilerServices;

namespace Froststrap.UI.Elements.Base
{
    internal abstract class AvaloniaWindow : Window
    {
        private sealed class BackdropState
        {
            public WindowsBackdrops? Applied;
        }

        private static readonly ConditionalWeakTable<Window, BackdropState> BackdropStates = new();

        private static IStyle? _activeColorStyle;
        private static ResourceDictionary? _activeThemeDictionary;
        private static string? _appliedThemeName;

        private static string? _cachedThemeName;
        private static ResourceDictionary? _cachedThemeDictionary;
        private static Styles? _cachedThemeStyles;

        private static IBrush _currentBackgroundBrush = Brushes.Transparent;
        private static Bitmap? _currentBackgroundBitmap;
        private static string? _currentBitmapPath;
        private static string? _requestedBitmapPath;
        private static string? _currentAnimatedImagePath;
        private static bool _currentIsAnimatedGif;
        private static Stretch _currentImageStretch = Stretch.UniformToFill;
        private static double _currentImageOpacity = 1.0;
        private static int _backgroundLoadVersion;

        private static readonly string[] AnimatedImageExtensions = [".gif"];

        private readonly Panel? _backgroundHost;
        private readonly Image? _backgroundImage;
        private readonly Border? _backgroundBorder;
        private readonly ContentPresenter? _contentHost;

        private bool _backgroundApplied;
        private bool _appliedShowImage;
        private string? _appliedAnimatedPath;
        private Bitmap? _appliedBitmap;
        private IBrush? _appliedBrush;
        private Stretch _appliedStretch;
        private double _appliedOpacity;

        protected virtual bool ApplyTopPadding => true;

        static AvaloniaWindow()
        {
            ContentProperty.OverrideMetadata<AvaloniaWindow>(
                new StyledPropertyMetadata<object?>(coerce: CoerceContent));
        }

        private static object? CoerceContent(AvaloniaObject sender, object? value)
        {
            if (sender is not AvaloniaWindow window || window._backgroundHost is null)
                return value;

            if (ReferenceEquals(value, window._backgroundHost))
                return value;

            window._contentHost?.Content = value;

            return window._backgroundHost;
        }

        public AvaloniaWindow()
        {
            var panel = new BackgroundHostPanel();

            _backgroundBorder = panel.Find<Border>("PART_BackgroundBorder");
            _backgroundImage = panel.Find<Image>("PART_BackgroundImage");
            _contentHost = panel.Find<ContentPresenter>("PART_ContentHost");
            _backgroundHost = panel;

            if (_backgroundImage != null)
                RenderOptions.SetBitmapInterpolationMode(_backgroundImage, BitmapInterpolationMode.HighQuality);

            ApplyWindowBackground();

            WindowDecorations = WindowDecorations.Full;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = OperatingSystem.IsMacOS() ? -1 : 40;
            MacOSTitleBar.SetTrafficLightOffset(this, new Point(20,19));
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
            if (ApplyTopPadding) _contentHost?.Margin = new Thickness(0, 36, 0, 0);

            ApplyTheme();
        }

        private void ApplyWindowBackground()
        {
            if (_backgroundHost == null || _backgroundImage == null || _backgroundBorder == null)
                return;

            bool showImage = _currentIsAnimatedGif || _currentBackgroundBitmap != null;

            if (_backgroundApplied
                && _appliedShowImage == showImage
                && ReferenceEquals(_appliedBitmap, _currentBackgroundBitmap)
                && string.Equals(_appliedAnimatedPath, _currentAnimatedImagePath, StringComparison.Ordinal)
                && ReferenceEquals(_appliedBrush, _currentBackgroundBrush)
                && _appliedStretch == _currentImageStretch
                && _appliedOpacity == _currentImageOpacity)
                return;

            string? previousAnimatedPath = _appliedAnimatedPath;

            _backgroundApplied = true;
            _appliedShowImage = showImage;
            _appliedAnimatedPath = _currentAnimatedImagePath;
            _appliedBitmap = _currentBackgroundBitmap;
            _appliedBrush = _currentBackgroundBrush;
            _appliedStretch = _currentImageStretch;
            _appliedOpacity = _currentImageOpacity;

            _backgroundHost.Background = null;

            if (showImage)
            {
                _backgroundBorder.Background = null;
                _backgroundImage.IsVisible = true;
                _backgroundImage.Stretch = _currentImageStretch;
                _backgroundImage.Opacity = _currentImageOpacity;

                if (_currentIsAnimatedGif && _currentAnimatedImagePath is not null)
                {
                    bool pathChanged = !string.Equals(previousAnimatedPath, _currentAnimatedImagePath, StringComparison.Ordinal);

                    if (pathChanged || ImageBehavior.GetAnimatedSource(_backgroundImage) is null)
                    {
                        var uri = new Uri(_currentAnimatedImagePath, UriKind.Absolute);
                        ImageBehavior.SetAnimatedSource(_backgroundImage, new AnimatedImageSourceUri(uri));
                        ImageBehavior.SetRepeatBehavior(_backgroundImage, RepeatBehavior.Forever);
                        _backgroundImage.Source = null;
                    }
                }
                else
                {
                    if (ImageBehavior.GetAnimatedSource(_backgroundImage) is not null)
                        ImageBehavior.SetAnimatedSource(_backgroundImage, null!);

                    _backgroundImage.Source = _currentBackgroundBitmap;
                }
            }
            else
            {
                _backgroundImage.IsVisible = false;

                if (ImageBehavior.GetAnimatedSource(_backgroundImage) is not null)
                    ImageBehavior.SetAnimatedSource(_backgroundImage, null!);

                _backgroundImage.Source = null;
                _backgroundBorder.Background = _currentBackgroundBrush;
            }
        }

        public static void ApplyTheme()
        {
            var app = Application.Current;

            if (app == null)
                return;

            var finalTheme = App.Settings.Prop.Theme.GetFinal();
            string themeName = Enum.GetName(finalTheme) ?? "Dark";
            bool isCustom = finalTheme == Enums.Theme.Custom;

            bool themeChanged = isCustom
                || _activeThemeDictionary == null
                || _activeColorStyle == null
                || !string.Equals(_appliedThemeName, themeName, StringComparison.Ordinal);

            if (!themeChanged)
            {
                UpdateBackdropForAllWindows();
                return;
            }

            app.RequestedThemeVariant = finalTheme == Enums.Theme.Light
                ? ThemeVariant.Light
                : ThemeVariant.Dark;

            var faTheme = app.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();

            if (faTheme != null)
            {
                faTheme.PreferSystemTheme = false;
                faTheme.PreferUserAccentColor = true;
            }

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

            IBrush? backgroundBrush = null;
            string? backgroundImagePath = null;
            bool isAnimatedGif = false;
            Stretch imageStretch = Stretch.UniformToFill;
            double imageOpacity = 1.0;

            if (!isCustom)
            {
                EnsureCachedTheme(themeName);

                _activeThemeDictionary = _cachedThemeDictionary;
                app.Resources.MergedDictionaries.Add(_cachedThemeDictionary!);

                if (_cachedThemeDictionary!.TryGetValue("ApplicationBackgroundColor", out var themeBg))
                    backgroundBrush = themeBg as IBrush;

                _activeColorStyle = _cachedThemeStyles;
                app.Styles.Insert(1, _cachedThemeStyles!);

                _appliedThemeName = themeName;
            }
            else
            {
                var customDict = new ResourceDictionary { ["NotificationBackgroundColor"] = new SolidColorBrush(Color.Parse("#2D2D2D")) };
                _activeThemeDictionary = customDict;
                app.Resources.MergedDictionaries.Add(customDict);
                _appliedThemeName = null;

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

                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        try
                        {
                            string extension = Path.GetExtension(path);
                            isAnimatedGif = AnimatedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
                            backgroundImagePath = path;
                            imageStretch = (Stretch)App.Settings.Prop.BackgroundStretch;
                            imageOpacity = App.Settings.Prop.BackgroundOpacity;
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Error($"Image load error: {ex}");
                        }
                    }
                }
            }

            _currentBackgroundBrush = backgroundBrush ?? Brushes.Transparent;
            _currentIsAnimatedGif = isAnimatedGif;
            _currentAnimatedImagePath = backgroundImagePath;
            _currentImageStretch = imageStretch;
            _currentImageOpacity = imageOpacity;

            string? bitmapPath = isAnimatedGif ? null : backgroundImagePath;
            Bitmap? bitmapToDispose = null;

            if (bitmapPath == null)
            {
                _requestedBitmapPath = null;
                _backgroundLoadVersion++;

                if (_currentBackgroundBitmap != null)
                {
                    bitmapToDispose = _currentBackgroundBitmap;
                    _currentBackgroundBitmap = null;
                    _currentBitmapPath = null;
                }
            }
            else if (string.Equals(bitmapPath, _currentBitmapPath, StringComparison.Ordinal))
            {
                _requestedBitmapPath = bitmapPath;
            }
            else if (!string.Equals(bitmapPath, _requestedBitmapPath, StringComparison.Ordinal))
            {
                _requestedBitmapPath = bitmapPath;
                StartBackgroundBitmapLoad(bitmapPath);
            }

            ApplyBackgroundToAllWindows();
            UpdateBackdropForAllWindows();

            bitmapToDispose?.Dispose();
        }

        private static void EnsureCachedTheme(string themeName)
        {
            if (string.Equals(_cachedThemeName, themeName, StringComparison.Ordinal)
                && _cachedThemeDictionary != null
                && _cachedThemeStyles != null)
                return;

            bool light = themeName == "Light";

            _cachedThemeDictionary = light
                ? new Froststrap.UI.AppThemes.ResourceDictionarys.Light()
                : new Froststrap.UI.AppThemes.ResourceDictionarys.Dark();

            _cachedThemeStyles = light
                ? new Froststrap.UI.AppThemes.Styles.Light()
                : new Froststrap.UI.AppThemes.Styles.Dark();

            _cachedThemeName = themeName;
        }

        private static void StartBackgroundBitmapLoad(string path)
        {
            int version = ++_backgroundLoadVersion;

            _ = Task.Run(() =>
            {
                Bitmap? bitmap = null;

                try
                {
                    bitmap = new Bitmap(path);
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Image load error: {ex}");
                }

                if (bitmap is null)
                    return;

                Dispatcher.UIThread.Post(() => CompleteBackgroundBitmapLoad(path, version, bitmap));
            });
        }

        private static void CompleteBackgroundBitmapLoad(string path, int version, Bitmap bitmap)
        {
            if (version != _backgroundLoadVersion
                || !string.Equals(path, _requestedBitmapPath, StringComparison.Ordinal))
            {
                bitmap.Dispose();
                return;
            }

            var previous = _currentBackgroundBitmap;

            _currentBackgroundBitmap = bitmap;
            _currentBitmapPath = path;

            ApplyBackgroundToAllWindows();

            if (previous != null && !ReferenceEquals(previous, bitmap))
                previous.Dispose();
        }

        private static void ApplyBackgroundToAllWindows()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            foreach (var window in desktop.Windows)
            {
                if (window is AvaloniaWindow avaloniaWindow)
                    avaloniaWindow.ApplyWindowBackground();
            }
        }

        public static void UpdateBackdropForAllWindows()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            var selectedBackdrop = App.Settings.Prop.SelectedBackdrop;

            foreach (var window in desktop.Windows)
            {
                if (window is null)
                    continue;

                var state = BackdropStates.GetValue(window, static _ => new BackdropState());

                if (state.Applied == selectedBackdrop)
                    continue;

                state.Applied = selectedBackdrop;

                if (selectedBackdrop != WindowsBackdrops.None)
                {
                    window.TransparencyLevelHint = selectedBackdrop switch
                    {
                        WindowsBackdrops.Acrylic => [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None],
                        WindowsBackdrops.Mica => [WindowTransparencyLevel.Mica, WindowTransparencyLevel.None],
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_backgroundImage != null)
            {
                if (ImageBehavior.GetAnimatedSource(_backgroundImage) is not null)
                    ImageBehavior.SetAnimatedSource(_backgroundImage, null!);

                _backgroundImage.Source = null;
            }

            _backgroundApplied = false;
            _appliedShowImage = false;
            _appliedAnimatedPath = null;
            _appliedBitmap = null;
            _appliedBrush = null;
            _appliedStretch = default;
            _appliedOpacity = 0;
        }
    }
}
