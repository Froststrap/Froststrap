using Bloxstrap.UI.Elements.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Bloxstrap.UI.ViewModels.Dialogs
{
    public partial class CommunityModInfoViewModel : ObservableObject
    {
        [ObservableProperty]
        private CommunityMod _mod;

        [ObservableProperty]
        private bool _isLoadingGlyphs = true;

        [ObservableProperty]
        private ObservableCollection<GlyphItem> _glyphItems = new();

        private WpfUiWindow _window;

        public CommunityModInfoViewModel(CommunityMod mod, WpfUiWindow window)
        {
            _mod = mod;
            _window = window;
            _ = LoadGlyphsAsync();
        }
        
        // copy and pasted most code from already existing preview in mod generator
        [RelayCommand]
        private async Task LoadGlyphsAsync()
        {
            try
            {
                string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
                string fontDir = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

                if (!Directory.Exists(fontDir))
                {
                    IsLoadingGlyphs = false;
                    return;
                }

                var fontFiles = Directory.GetFiles(fontDir)
                    .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (fontFiles.Length == 0)
                {
                    IsLoadingGlyphs = false;
                    return;
                }

                await LoadGlyphsFromFontAsync(fontFiles[0]);
            }
            catch
            {
                IsLoadingGlyphs = false;
            }
        }

        private async Task LoadGlyphsFromFontAsync(string fontPath)
        {
            try
            {
                var glyphTypeface = new GlyphTypeface(new Uri(fontPath));
                var glyphItems = new List<GlyphItem>();

                SolidColorBrush colorBrush;
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(Mod.HexCode ?? "#FFFFFF");
                    colorBrush = new SolidColorBrush(color);
                    colorBrush.Freeze();
                }
                catch
                {
                    colorBrush = new SolidColorBrush(Colors.White);
                    colorBrush.Freeze();
                }

                var characterCodes = glyphTypeface.CharacterToGlyphMap.Keys
                    .OrderByDescending(c => c)
                    .Take(100)
                    .ToList();

                foreach (var characterCode in characterCodes)
                {
                    if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(characterCode, out ushort glyphIndex))
                        continue;

                    var geometry = glyphTypeface.GetGlyphOutline(glyphIndex, 40, 40);
                    var bounds = geometry.Bounds;
                    var translate = new TranslateTransform(
                        (50 - bounds.Width) / 2 - bounds.X,
                        (50 - bounds.Height) / 2 - bounds.Y
                    );
                    geometry.Transform = translate;

                    glyphItems.Add(new GlyphItem
                    {
                        Data = geometry,
                        ColorBrush = colorBrush
                    });
                }

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    GlyphItems.Clear();
                    foreach (var item in glyphItems)
                    {
                        GlyphItems.Add(item);
                    }
                    IsLoadingGlyphs = false;
                });
            }
            catch
            {
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsLoadingGlyphs = false;
                });
            }
        }
    }
}