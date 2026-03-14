using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Base;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public partial class CommunityModInfoViewModel : ObservableObject
    {
        [ObservableProperty] private CommunityMod _mod;
        [ObservableProperty] private bool _isLoadingGlyphs = false;
        [ObservableProperty] private ObservableCollection<GlyphItem> _glyphItems = new();

        private readonly AvaloniaWindow _window;

        public CommunityModInfoViewModel(CommunityMod mod, AvaloniaWindow window)
        {
            _mod = mod;
            _window = window;

            if (_mod.IsColorMod)
                _ = LoadGlyphsAsync();
        }

        [RelayCommand]
        private void Close() => _window.Close();

        private async Task LoadGlyphsAsync()
        {
            IsLoadingGlyphs = true;
            try
            {
                string fontDir = Path.Combine(Path.GetTempPath(), "Froststrap", "Fonts");
                Directory.CreateDirectory(fontDir);
                string fontPath = Path.Combine(fontDir, "BuilderIcons-Regular.ttf");

                if (!File.Exists(fontPath))
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    var data = await httpClient.GetByteArrayAsync("https://raw.githubusercontent.com/RealMeddsam/config/main/BuilderIcons-Regular.ttf");
                    await File.WriteAllBytesAsync(fontPath, data);
                }

                await GenerateGlyphPreviews(fontPath);
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("CommunityModInfoViewModel", ex);
            }
            finally
            {
                IsLoadingGlyphs = false;
            }
        }

        private async Task GenerateGlyphPreviews(string fontPath)
        {
            var typeface = new Typeface(new Uri($"file://{fontPath}"));

            var color = Colors.White;
            try
            {
                if (!string.IsNullOrEmpty(Mod.HexCode))
                    color = Color.Parse(Mod.HexCode);
            }
            catch { /* Fallback to white */ }

            var brush = new SolidColorBrush(color);

            var fontManager = FontManager.Current;

            var codes = Enumerable.Range(33, 126)
                .OrderByDescending(c => c)
                .Take(25)
                .ToList();

            var items = new List<GlyphItem>();

            foreach (var code in codes)
            {
                var text = char.ConvertFromUtf32(code);

                var geometry = StreamGeometry.Parse("");

                var ft = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    40,
                    brush);

                var glyphGeometry = ft.BuildGeometry(new Avalonia.Point(0, 0));

                items.Add(new GlyphItem
                {
                    Data = glyphGeometry,
                    ColorBrush = brush
                });
            }

            await Dispatcher.UIThread.InvokeAsync(() => {
                GlyphItems = new ObservableCollection<GlyphItem>(items);
            });
        }
    }
}