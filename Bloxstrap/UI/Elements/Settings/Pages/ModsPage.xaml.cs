/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 *  Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
 *               of the Nix ecosystem. 
 */

using Bloxstrap.Integrations;
using Bloxstrap.UI.ViewModels.Settings;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Drawing.Color;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class ModsPage
    {
        private ModsViewModel ViewModel;
        private Color _solidColor = Color.White;
        public ModsPage()
        {
            InitializeComponent();

            ViewModel = new ModsViewModel();
            DataContext = this;

            IncludeModificationsCheckBox.IsChecked = true;

            SolidColorTextBox.Text = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}";
            LoadFontFiles();
        }

        private string[] _fontFiles = Array.Empty<string>();

        private async void LoadFontFiles()
        {
            string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
            string fontDir = Path.Combine(froststrapTemp, @"LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

            if (!Directory.Exists(fontDir))
                return;

            _fontFiles = Directory.GetFiles(fontDir, "*.ttf");
            FontSelectorComboBox.ItemsSource = _fontFiles.Select(f => Path.GetFileName(f));

            if (_fontFiles.Length > 0)
                FontSelectorComboBox.SelectedIndex = 0;
        }

        private async void FontSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSelectorComboBox.SelectedIndex < 0 || FontSelectorComboBox.SelectedIndex >= _fontFiles.Length)
                return;

            string selectedFont = _fontFiles[FontSelectorComboBox.SelectedIndex];
            await LoadGlyphPreviewsAsync(selectedFont);
        }

        private async void ModGenerator_Click(object sender, RoutedEventArgs e)
        {
            const string LOG_IDENT = "UI::ModGenerator";
            var overallSw = Stopwatch.StartNew();

            GenerateModButton.IsEnabled = false;

            DownloadStatusText.Text = "Starting mod generation...";
            App.Logger?.WriteLine(LOG_IDENT, "Mod generation started.");

            try
            {
                var (luaPackagesZip, extraTexturesZip, contentTexturesZip, versionHash, version) =
                    await ModGenerator.DownloadForModGenerator();

                App.Logger?.WriteLine(LOG_IDENT, $"DownloadForModGenerator returned. Version: {version} ({versionHash})");

                string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");

                string luaPackagesDir = Path.Combine(froststrapTemp, "ExtraContent", "LuaPackages");
                string extraTexturesDir = Path.Combine(froststrapTemp, "ExtraContent", "textures");
                string contentTexturesDir = Path.Combine(froststrapTemp, "content", "textures");

                void SafeExtract(string zipPath, string targetDir)
                {
                    if (Directory.Exists(targetDir))
                    {
                        try { Directory.Delete(targetDir, true); }
                        catch (Exception ex)
                        {
                            App.Logger?.WriteException(LOG_IDENT, ex);
                            throw;
                        }
                    }

                    Directory.CreateDirectory(targetDir);

                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.FullName) || entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                                continue;

                            string destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

                            if (!destinationPath.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                                throw new IOException($"Entry {entry.FullName} is trying to extract outside of {targetDir}");

                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                }

                DownloadStatusText.Text = "Extracting ZIPs...";
                App.Logger?.WriteLine(LOG_IDENT, "Extracting downloaded ZIPs...");

                SafeExtract(luaPackagesZip, luaPackagesDir);
                SafeExtract(extraTexturesZip, extraTexturesDir);
                SafeExtract(contentTexturesZip, contentTexturesDir);

                App.Logger?.WriteLine(LOG_IDENT, "Extraction complete.");

                var assembly = Assembly.GetExecutingAssembly();
                Dictionary<string, string[]> mappings;
                using (var stream = assembly.GetManifestResourceStream("Bloxstrap.Resources.mappings.json"))
                using (var reader = new StreamReader(stream!))
                {
                    string json = await reader.ReadToEndAsync();
                    mappings = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)!;
                }
                App.Logger?.WriteLine(LOG_IDENT, $"Loaded mappings.json with {mappings.Count} top-level entries.");

                DownloadStatusText.Text = "Recoloring images...";

                bool colorCursors = CursorsCheckBox?.IsChecked == true;
                bool colorShiftlock = ShiftlockCheckBox?.IsChecked == true;
                bool colorEmoteWheel = EmoteWheelCheckBox?.IsChecked == true;
                bool colorVoiceChat = VoiceChatCheckBox?.IsChecked == true;

                App.Logger?.WriteLine(LOG_IDENT, $"Using solid color for recolor: {_solidColor}");

                App.Logger?.WriteLine(LOG_IDENT, "Starting RecolorAllPngs...");
                ModGenerator.RecolorAllPngs(froststrapTemp, _solidColor, colorCursors, colorShiftlock, colorEmoteWheel, colorVoiceChat);
                App.Logger?.WriteLine(LOG_IDENT, "RecolorAllPngs finished.");

                // you need to have python and install fonttools to recolor fonts
                try
                {
                    DownloadStatusText.Text = "Recoloring fonts...";
                    App.Logger?.WriteLine(LOG_IDENT, "Starting font recoloring...");

                    await ModGenerator.RecolorFontsAsync(froststrapTemp, _solidColor);
                    App.Logger?.WriteLine(LOG_IDENT, "Font recoloring finished.");

                    ModGenerator.UpdateBuilderIconsJson(froststrapTemp);
                }
                catch (Exception ex)
                {
                    App.Logger?.WriteException(LOG_IDENT, ex);
                    DownloadStatusText.Text += "Font recoloring failed but continuing";
                }

                DownloadStatusText.Text = "Cleaning up unnecessary files...";
                var preservePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in mappings.Values)
                {
                    string fullPath = Path.Combine(froststrapTemp, Path.Combine(entry));
                    preservePaths.Add(fullPath);
                }

                preservePaths.Add(Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\BuilderIcons.json"));

                string builderIconsFontDir = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");
                if (Directory.Exists(builderIconsFontDir))
                {
                    preservePaths.Add(builderIconsFontDir);
                    var fontFiles = Directory.GetFiles(builderIconsFontDir, "*.*");
                    foreach (var fontFile in fontFiles)
                    {
                        preservePaths.Add(fontFile);
                    }
                }

                if (colorCursors)
                {
                    preservePaths.Add(Path.Combine(froststrapTemp, @"content\textures\Cursors\KeyboardMouse\IBeamCursor.png"));
                    preservePaths.Add(Path.Combine(froststrapTemp, @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png"));
                    preservePaths.Add(Path.Combine(froststrapTemp, @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png"));
                }

                if (colorShiftlock)
                {
                    preservePaths.Add(Path.Combine(froststrapTemp, @"content\textures\MouseLockedCursor.png"));
                }

                if (colorEmoteWheel)
                {
                    string emotesDir = Path.Combine(froststrapTemp, @"content\textures\ui\Emotes\Large");
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedGradient.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedGradient@2x.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedGradient@3x.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedLine.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedLine@2x.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedLine@3x.png"));
                }

                if (colorVoiceChat)
                {
                    var voiceChatMappings = new Dictionary<string, (string BaseDir, string[] Files)>
                    {
                        ["VoiceChat"] = (
                            @"content\textures\ui\VoiceChat",
                            new[]
                            {"Blank.png","Blank@2x.png","Blank@3x.png","Error.png","Error@2x.png","Error@3x.png","Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png"}
                        ),
                        ["SpeakerNew"] = (
                            @"content\textures\ui\VoiceChat\SpeakerNew",
                            new[]
                            {"Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Error.png","Error@2x.png","Error@3x.png","Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png"}
                        ),
                        ["SpeakerLight"] = (
                            @"content\textures\ui\VoiceChat\SpeakerLight",
                            new[]
                            {"Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Error.png","Error@2x.png","Error@3x.png","Muted.png"}
                        ),
                        ["SpeakerDark"] = (
                            @"content\textures\ui\VoiceChat\SpeakerDark",
                            new[]
                            {"Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Error.png","Error@2x.png","Error@3x.png","Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png"}
                        ),
                        ["RedSpeakerLight"] = (
                            @"content\textures\ui\VoiceChat\RedSpeakerLight",
                            new[]
                            {"Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png"}
                        ),
                        ["RedSpeakerDark"] = (
                            @"content\textures\ui\VoiceChat\RedSpeakerDark",
                            new[]
                            {"Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png"}
                        ),
                        ["New"] = (
                            @"content\textures\ui\VoiceChat\New",
                            new[]
                            {"Error.png","Error@2x.png","Error@3x.png", "Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Blank.png","Blank@2x.png","Blank@3x.png"}
                        ),
                        ["MicLight"] = (
                            @"content\textures\ui\VoiceChat\MicLight",
                            new[]
                            {"Error.png","Error@2x.png","Error@3x.png","Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png"}
                        ),
                        ["MicDark"] = (
                            @"content\textures\ui\VoiceChat\MicDark",
                            new[]
                            {"Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Error.png","Error@2x.png","Error@3x.png"}
                        )
                    };

                    foreach (var mapping in voiceChatMappings.Values)
                    {
                        string baseDir = Path.Combine(froststrapTemp, mapping.BaseDir);
                        foreach (var file in mapping.Files)
                        {
                            preservePaths.Add(Path.Combine(baseDir, file));
                        }
                    }
                }

                void DeleteExcept(string dir)
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        if (!preservePaths.Contains(file))
                        {
                            try { File.Delete(file); } catch { /* ignore */ }
                        }
                    }

                    foreach (var subDir in Directory.GetDirectories(dir))
                    {
                        if (!preservePaths.Contains(subDir))
                        {
                            try
                            {
                                DeleteExcept(subDir);
                                if (Directory.Exists(subDir) && !Directory.EnumerateFileSystemEntries(subDir).Any())
                                    Directory.Delete(subDir);
                            }
                            catch { /* ignore */ }
                        }
                    }
                }

                var otfFiles = Directory.GetFiles(froststrapTemp, "*.otf", SearchOption.TopDirectoryOnly);

                foreach (var otfFile in otfFiles)
                {
                    File.Delete(otfFile);
                }

                if (Directory.Exists(luaPackagesDir)) DeleteExcept(luaPackagesDir);
                if (Directory.Exists(extraTexturesDir)) DeleteExcept(extraTexturesDir);
                if (Directory.Exists(contentTexturesDir)) DeleteExcept(contentTexturesDir);

                string infoPath = Path.Combine(froststrapTemp, "info.json");
                var infoData = new
                {
                    FroststrapVersion = App.Version,
                    CreatedUsing = "Froststrap",
                    RobloxVersion = version,
                    RobloxVersionHash = versionHash,
                    OptionsUsed = new
                    {
                        ColorCursors = colorCursors,
                        ColorShiftlock = colorShiftlock,
                        ColorVoicechat = colorVoiceChat,
                        ColorEmoteWheel = colorEmoteWheel,
                    },
                    ColorsUsed = new
                    {
                        SolidColor = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}"
                    }
                };

                string infoJson = JsonSerializer.Serialize(infoData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(infoPath, infoJson);

                if (IncludeModificationsCheckBox.IsChecked == true)
                {
                    if (!Directory.Exists(Paths.Modifications))
                        Directory.CreateDirectory(Paths.Modifications);

                    int copiedFiles = 0;
                    foreach (var file in Directory.GetFiles(froststrapTemp, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = Path.GetRelativePath(froststrapTemp, file);
                        string targetPath = Path.Combine(Paths.Modifications, relativePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        File.Copy(file, targetPath, overwrite: true);
                        copiedFiles++;
                    }

                    DownloadStatusText.Text = $"Mod generation completed! Copied {copiedFiles} files to Modifications folder.";
                }
                else
                {
                    DownloadStatusText.Text = "Mod generation completed!";
                }

                App.Logger?.WriteLine(LOG_IDENT, $"Mod generation finished in {overallSw.Elapsed.TotalSeconds:0.00}s");
            }
            catch (Exception ex)
            {
                DownloadStatusText.Text = $"Mod generation failed: {ex.Message}";
                App.Logger?.WriteException(LOG_IDENT, ex);
            }
            finally
            {
                GenerateModButton.IsEnabled = true;
            }
        }

        private void OnSolidColorChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string hex = SolidColorTextBox.Text.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;

                var col = ColorTranslator.FromHtml(hex);
                _solidColor = col;

            }
            catch
            {
                // ignore invalid input
            }
        }

        private async void OnChangeSolidColor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.ColorDialog
            {
                AllowFullOpen = true,
                FullOpen = true,
                Color = _solidColor
            };

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            _solidColor = dlg.Color;
            SolidColorTextBox.Text = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}";
        }

        private async Task LoadGlyphPreviewsAsync(string fontPath)
        {
            const string LOG_IDENT = "UI::LoadGlyphPreviewAsync";
            App.Logger?.WriteLine(LOG_IDENT, $"Loading glyph previews for font: {fontPath}");

            var glyphGeometries = new ConcurrentBag<Geometry>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount / 4);

            try
            {
                var typeface = new GlyphTypeface(new Uri(fontPath));
                var tasks = typeface.CharacterToGlyphMap.Values.Select(async glyphIndex =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var geometry = typeface.GetGlyphOutline(glyphIndex, 40, 40);

                        var bounds = geometry.Bounds;
                        var translate = new TranslateTransform(
                            (50 - bounds.Width) / 2 - bounds.X,
                            (50 - bounds.Height) / 2 - bounds.Y
                        );
                        geometry.Transform = translate;

                        glyphGeometries.Add(geometry);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);

                App.Current.Dispatcher.Invoke(() =>
                {
                    GlyphGrid.ItemsSource = glyphGeometries;
                });

                App.Logger?.WriteLine(LOG_IDENT, $"Loaded {glyphGeometries.Count} glyph previews.");
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT, ex);
            }
        }

        private async void LoadGlyphsButton_Click(object sender, RoutedEventArgs e)
        {
            const string LOG_IDENT = "UI::LoadGlyphsButton";
            try
            {
                string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
                string fontDir = Path.Combine(froststrapTemp, @"LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

                if (!Directory.Exists(fontDir))
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Font directory not found: {fontDir}");
                    return;
                }

                var fontFiles = Directory.GetFiles(fontDir, "*.ttf");
                if (fontFiles.Length == 0)
                {
                    App.Logger?.WriteLine(LOG_IDENT, "No .ttf font files found in font directory.");
                    return;
                }

                string fontFile = fontFiles.First();
                await LoadGlyphPreviewsAsync(fontFile);
                App.Logger?.WriteLine(LOG_IDENT, $"Glyphs loaded from {fontFile}");
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT, ex);
            }
        }
    }
}
