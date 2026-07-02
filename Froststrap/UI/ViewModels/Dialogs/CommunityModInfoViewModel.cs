using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Base;
using System.Collections.ObjectModel;
using AvaFontFamily = Avalonia.Media.FontFamily;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public partial class CommunityModInfoViewModel(CommunityMod mod, AvaloniaWindow window) : NotifyPropertyChangedViewModel
    {
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

        private ObservableCollection<GlyphItem> _glyphItems = [];
        public ObservableCollection<GlyphItem> GlyphItems
        {
            get => _glyphItems;
            set => SetProperty(ref _glyphItems, value);
        }

        private SolidColorBrush _previewBrush = new(Colors.White);
        public SolidColorBrush PreviewBrush
        {
            get => _previewBrush;
            set => SetProperty(ref _previewBrush, value);
        }

        public bool IsGradientMode => Mod.GradientStops?.Count >= 2;

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
                var fontFamily = new AvaFontFamily("avares://Froststrap/Resources/Fonts#BuilderIcons-Regular");
                var typeface = new Typeface(fontFamily);

                UpdateGlyphColors();
                await LoadGlyphPreviewsAsync(typeface);
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine("CommunityModInfoViewModel", $"Preview initialization failed: {ex.Message}");
            }
        }

        private void UpdateGlyphColors()
        {
            if (IsGradientMode)
            {
                var stops = Mod.GradientStops!;
                ColorDisplayText = string.Join(" → ", stops.Select(s => s.Color.ToUpper()));

                if (stops.Count > 0 && Color.TryParse(stops[0].Color, out var firstColor))
                    PreviewBrush = new SolidColorBrush(firstColor);
                else
                    PreviewBrush = new SolidColorBrush(Colors.White);
                return;
            }

            if (!string.IsNullOrEmpty(Mod.HexCode) && Color.TryParse(Mod.HexCode, out var hexColor))
            {
                ColorDisplayText = Mod.HexCode.ToUpper();
                PreviewBrush = new SolidColorBrush(hexColor);
                return;
            }

            ColorDisplayText = "No color information";
            PreviewBrush = new SolidColorBrush(Colors.White);
        }

        private IBrush CreateGradientBrush()
        {
            if (!IsGradientMode || Mod.GradientStops == null || Mod.GradientStops.Count < 2)
                return PreviewBrush;

            var stops = Mod.GradientStops.OrderBy(s => s.Offset).ToList();
            var avaloniaStops = new Avalonia.Media.GradientStops();
            foreach (var stop in stops)
            {
                if (Color.TryParse(stop.Color, out var color))
                    avaloniaStops.Add(new Avalonia.Media.GradientStop(color, stop.Offset));
            }
            if (avaloniaStops.Count < 2)
                return PreviewBrush;

            var brush = new LinearGradientBrush
            {
                GradientStops = avaloniaStops
            };

            double angle = Mod.GradientAngle ?? 90;
            double angleRad = angle * Math.PI / 180.0;
            double dx = Math.Sin(angleRad);
            double dy = -Math.Cos(angleRad);
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 0)
            {
                dx /= len;
                dy /= len;
            }
            brush.StartPoint = new RelativePoint(0.5 - dx / 2, 0.5 - dy / 2, RelativeUnit.Relative);
            brush.EndPoint = new RelativePoint(0.5 + dx / 2, 0.5 + dy / 2, RelativeUnit.Relative);

            return brush;
        }

        private async Task LoadGlyphPreviewsAsync(Typeface typeface)
        {
            IsLoadingGlyphs = true;
            var newItems = new ObservableCollection<GlyphItem>();

            try
            {
                IBrush glyphBrush = IsGradientMode ? CreateGradientBrush() : PreviewBrush;

                var characterCodes = Enumerable.Range(0xF101, 50);

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
                                Brush = glyphBrush
                            });
                        }
                        catch
                        {
                            // Silently skip individual glyph errors
                        }
                    });
                }

                GlyphItems = newItems;
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine("CommunityModInfoViewModel", $"Glyph loading error: {ex.Message}");
            }
            finally
            {
                IsLoadingGlyphs = false;
            }
        }
    }
}