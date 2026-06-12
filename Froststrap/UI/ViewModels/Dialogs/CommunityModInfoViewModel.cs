using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Base;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using AvaFontFamily = Avalonia.Media.FontFamily;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public partial class CommunityModInfoViewModel(CommunityMod mod, AvaloniaWindow window) : NotifyPropertyChangedViewModel
    {
        private static readonly string FontDir = Path.Combine(Path.GetTempPath(), "Froststrap", "Fonts");

        private string _colorDisplayText = string.Empty;
        public string ColorDisplayText
        {
            get => _colorDisplayText;
            set => SetProperty(ref _colorDisplayText, value);
        }

        private CommunityMod _mod = mod;
        public CommunityMod Mod
        {
            get => _mod;
            set => SetProperty(ref _mod, value);
        }

        private bool _isLoadingGlyphs;
        public bool IsLoadingGlyphs
        {
            get => _isLoadingGlyphs;
            set => SetProperty(ref _isLoadingGlyphs, value);
        }

        private string _statusText = string.Empty;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private ObservableCollection<GlyphItem> _glyphItems = [];
        public ObservableCollection<GlyphItem> GlyphItems
        {
            get => _glyphItems;
            set => SetProperty(ref _glyphItems, value);
        }

        private IBrush _previewBrush = Brushes.White;
        public IBrush PreviewBrush
        {
            get => _previewBrush;
            set => SetProperty(ref _previewBrush, value);
        }

        public void Initialize()
        {
            if (Mod.IsColorMod)
                _ = InitializePreviewAsync();
        }

        [RelayCommand]
        private void Close() => window.Close();

        private async Task InitializePreviewAsync()
        {
            try
            {
                if (!Directory.Exists(FontDir))
                    Directory.CreateDirectory(FontDir);

                string fontPath = Path.Combine(FontDir, "BuilderIcons-Regular.ttf");

                if (!File.Exists(fontPath))
                {
                    StatusText = "Downloading preview assets...";
                    var data = await App.HttpClient.GetByteArrayAsync("https://raw.githubusercontent.com/RealMeddsam/config/main/BuilderIcons-Regular.ttf");
                    await File.WriteAllBytesAsync(fontPath, data);
                }

                UpdateGlyphColors();
                await LoadGlyphPreviewsAsync(fontPath);
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine("CommunityModInfoViewModel", $"Failed to initialize: {ex.Message}");
                StatusText = "Failed to load preview.";
            }
        }

        private void UpdateGlyphColors()
        {
            if (Mod.GradientStops != null && Mod.GradientStops.Count > 0)
            {
                ColorDisplayText = string.Join(" → ", Mod.GradientStops.Select(s => s.Color.ToUpper()));

                var stops = new Avalonia.Media.GradientStops();
                foreach (var stop in Mod.GradientStops.OrderBy(s => s.Offset))
                {
                    if (Color.TryParse(stop.Color, out var color))
                        stops.Add(new Avalonia.Media.GradientStop(color, stop.Offset));
                }
                if (stops.Count > 0)
                {
                    double angle = Mod.GradientAngle ?? 90;
                    var brush = new LinearGradientBrush
                    {
                        GradientStops = stops,
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
                    };
                    double rad = (angle - 90) * Math.PI / 180.0;
                    double dx = Math.Cos(rad);
                    double dy = Math.Sin(rad);
                    brush.StartPoint = new RelativePoint(0.5 - dx / 2, 0.5 - dy / 2, RelativeUnit.Relative);
                    brush.EndPoint = new RelativePoint(0.5 + dx / 2, 0.5 + dy / 2, RelativeUnit.Relative);
                    PreviewBrush = brush;
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(Mod.HexCode) && Color.TryParse(Mod.HexCode, out var color))
            {
                ColorDisplayText = Mod.HexCode.ToUpper();
                PreviewBrush = new SolidColorBrush(color);
                return;
            }

            ColorDisplayText = "No color information";
            PreviewBrush = Brushes.White;
        }

        private static bool IsFileReady(string filename)
        {
            try
            {
                using var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
                return fs.Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private async Task LoadGlyphPreviewsAsync(string fontPath)
        {
            if (!File.Exists(fontPath) || !IsFileReady(fontPath)) return;

            IsLoadingGlyphs = true;
            ObservableCollection<GlyphItem> newItems = [];
            UpdateGlyphColors();

            try
            {
                string variantName = Path.GetFileNameWithoutExtension(fontPath);
                AvaFontFamily? fontFamily = null;

                if (Application.Current != null)
                {
                    string resourceKey = variantName.EndsWith("Filled") ? "BuilderIconsFilled" : "BuilderIconsRegular";
                    if (Application.Current.Resources.TryGetResource(resourceKey, null, out object? res) && res is AvaFontFamily ff)
                    {
                        fontFamily = ff;
                    }
                }

                if (fontFamily == null)
                {
                    var fontUri = new Uri($"file:///{fontPath.Replace('\\', '/')}");
                    fontFamily = new AvaFontFamily(fontUri, "BuilderIcons");
                }

                var typeface = new Typeface(fontFamily);
                var characterCodes = Enumerable.Range(0xF101, 25);

                foreach (var characterCode in characterCodes)
                {
                    string text = char.ConvertFromUtf32(characterCode);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            var ft = new FormattedText(
                                text,
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                40,
                                PreviewBrush);

                            var geometry = ft.BuildGeometry(new Point(0, 0));
                            if (geometry == null || geometry.Bounds.Width < 1) return;

                            var bounds = geometry.Bounds;
                            var translate = new TranslateTransform(
                                (50 - bounds.Width) / 2 - bounds.X,
                                (50 - bounds.Height) / 2 - bounds.Y
                            );
                            geometry.Transform = translate;

                            newItems.Add(new GlyphItem
                            {
                                Data = geometry,
                                Brush = PreviewBrush
                            });
                        }
                        catch (Exception ex)
                        {
                            App.Logger?.WriteLine("CommunityModInfoViewModel", $"Glyph Error: {ex.Message}");
                        }
                    });
                }

                GlyphItems = newItems;
                StatusText = "Preview loaded.";
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine("CommunityModInfoViewModel", $"Load Error: {ex}");
                StatusText = "Failed to load glyphs.";
            }
            finally
            {
                IsLoadingGlyphs = false;
            }
        }
    }
}