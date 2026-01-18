using Avalonia.Media;
using Avalonia.Platform;
using Froststrap.UI.Elements.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public partial class CommunityModInfoViewModel : ObservableObject
    {
        [ObservableProperty]
        private CommunityMod _mod;

        [ObservableProperty]
        private bool _isLoadingGlyphs = true;

        [ObservableProperty]
        private ObservableCollection<GlyphItem> _glyphItems = new();

        private AvaloniaWindow _window;

        public CommunityModInfoViewModel(CommunityMod mod, AvaloniaWindow window)
        {
            _mod = mod;
            _window = window;
            _ = LoadGlyphsAsync();
        }

        [RelayCommand]
        private async Task LoadGlyphsAsync()
        {
            try
            {
                string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
                string fontDir = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

                if (!Directory.Exists(fontDir))
                {
                    Directory.CreateDirectory(fontDir);
                }

                string fontPath = Path.Combine(fontDir, "BuilderIcons-Regular.ttf");

                if (!File.Exists(fontPath))
                {
                    try
                    {
                        const string fontUrl = "https://raw.githubusercontent.com/RealMeddsam/config/main/BuilderIcons-Regular.ttf";

                        App.Logger?.WriteLine("CommunityModInfoViewModel", "Downloading font from GitHub...");

                        using var httpClient = new HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(30);

                        var response = await httpClient.GetAsync(fontUrl);
                        response.EnsureSuccessStatusCode();

                        var fontData = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(fontPath, fontData);

                        App.Logger?.WriteLine("CommunityModInfoViewModel", "Successfully downloaded BuilderIcons-Regular.ttf");
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteException("CommunityModInfoViewModel", ex);
                    }
                }

                if (File.Exists(fontPath))
                {
                    await LoadGlyphsFromFontAsync(fontPath);
                }
                else
                {
                    IsLoadingGlyphs = false;
                }
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
                var fontBytes = await File.ReadAllBytesAsync(fontPath);

                var fontUri = new Uri($"file:///{fontPath.Replace("\\", "/")}");
                var fontFamily = new Avalonia.Media.FontFamily($"{fontUri}#BuilderIcons");
                var typeface = new Typeface(fontFamily);

                var glyphItems = new List<GlyphItem>();

                SolidColorBrush colorBrush;
                try
                {
                    var color = Color.Parse(Mod.HexCode ?? "#FFFFFF");
                    colorBrush = new SolidColorBrush(color);
                }
                catch
                {
                    colorBrush = new SolidColorBrush(Colors.White);
                }

                var characterCodes = new List<int>();

                for (int i = 0xE000; i <= 0xF8FF; i++)
                {
                    characterCodes.Add(i);
                    if (characterCodes.Count >= 100)
                        break;
                }

                foreach (var characterCode in characterCodes)
                {
                    try
                    {
                        var character = char.ConvertFromUtf32(characterCode);

                        var formattedText = new FormattedText(
                            character,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            40,
                            colorBrush
                        );

                        var geometry = formattedText.BuildGeometry(new Avalonia.Point(0, 0));

                        if (geometry != null)
                        {
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
                    }
                    catch
                    {
                        continue;
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GlyphItems.Clear();
                    foreach (var item in glyphItems)
                    {
                        GlyphItems.Add(item);
                    }
                    IsLoadingGlyphs = false;
                });
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("CommunityModInfoViewModel::LoadGlyphsFromFontAsync", ex);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsLoadingGlyphs = false;
                });
            }
        }
    }
}