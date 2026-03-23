using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Base;
using Froststrap.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using AvaFontFamily = Avalonia.Media.FontFamily;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public partial class CommunityModInfoViewModel : ObservableObject
    {
        private static readonly string FontDir = Path.Combine(Path.GetTempPath(), "Froststrap", "Fonts");

        [ObservableProperty] private CommunityMod _mod;
        [ObservableProperty] private bool _isLoadingGlyphs = false;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private ObservableCollection<GlyphItem> _glyphItems = new();

        [ObservableProperty] private IBrush _previewBrush = Brushes.White;

        private readonly AvaloniaWindow _window;

        public CommunityModInfoViewModel(CommunityMod mod, AvaloniaWindow window)
        {
            _mod = mod;
            _window = window;

            if (_mod.IsColorMod)
                _ = InitializePreviewAsync();
        }

        [RelayCommand]
        private void Close() => _window.Close();

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
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    var data = await httpClient.GetByteArrayAsync("https://raw.githubusercontent.com/RealMeddsam/config/main/BuilderIcons-Regular.ttf");
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
            if (Color.TryParse(Mod.HexCode, out var color))
                PreviewBrush = new SolidColorBrush(color);
            else
                PreviewBrush = Brushes.White;
        }

        private bool IsFileReady(string filename)
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
            var newItems = new ObservableCollection<GlyphItem>();
            UpdateGlyphColors();

            try
            {
                string variantName = Path.GetFileNameWithoutExtension(fontPath);
                AvaFontFamily? fontFamily = null;

                if (Avalonia.Application.Current != null)
                {
                    string resourceKey = variantName.EndsWith("Filled") ? "BuilderIconsFilled" : "BuilderIconsRegular";
                    if (Avalonia.Application.Current.Resources.TryGetResource(resourceKey, null, out object? res) && res is AvaFontFamily ff)
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
                var characterCodes = Enumerable.Range(0xF101, 25).ToList();

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

                            var finalBrush = PreviewBrush as SolidColorBrush ?? new SolidColorBrush(Colors.White);

                            newItems.Add(new GlyphItem
                            {
                                Data = geometry,
                                ColorBrush = finalBrush
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