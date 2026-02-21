using Bloxstrap.UI.Elements.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Bloxstrap.UI.ViewModels.Dialogs
{
    public partial class CommunityModInfoViewModel : ObservableObject
    {
        [ObservableProperty] private CommunityMod _mod;
        [ObservableProperty] private bool _isLoadingGlyphs = false;
        [ObservableProperty] private ObservableCollection<GlyphItem> _glyphItems = new();

        private readonly WpfUiWindow _window;

        public CommunityModInfoViewModel(CommunityMod mod, WpfUiWindow window)
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
            var glyphTypeface = new GlyphTypeface(new Uri(fontPath));
            var color = Colors.White;

            try { color = (Color)ColorConverter.ConvertFromString(Mod.HexCode ?? "#FFFFFF"); } catch { }
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var codes = glyphTypeface.CharacterToGlyphMap.Keys.OrderByDescending(c => c).Take(25).ToList();
            var items = new List<GlyphItem>();

            foreach (var code in codes)
            {
                if (glyphTypeface.CharacterToGlyphMap.TryGetValue(code, out ushort index))
                {
                    var geometry = glyphTypeface.GetGlyphOutline(index, 40, 40);
                    geometry.Freeze();
                    items.Add(new GlyphItem { Data = geometry, ColorBrush = brush });
                }
            }

            await App.Current.Dispatcher.InvokeAsync(() => {
                GlyphItems = new ObservableCollection<GlyphItem>(items);
            });
        }
    }
}