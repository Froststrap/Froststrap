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
using Microsoft.Win32;
using System.ComponentModel;
using System.Drawing;
using ICSharpCode.SharpZipLib.Zip;
using System.IO.Compression;
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
        private List<GlyphItem> _glyphItems = new List<GlyphItem>();

        public ModsPage()
        {
            InitializeComponent();

            ViewModel = new ModsViewModel();
            DataContext = ViewModel;

            IncludeModificationsCheckBox.IsChecked = true;

            SolidColorTextBox.Text = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}";
            LoadFontFiles();
        }

        private string[] _fontFiles = Array.Empty<string>();

        private async void LoadFontFiles()
        {
            string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
            string fontDir = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

            if (!Directory.Exists(fontDir))
            {
                Directory.CreateDirectory(fontDir);
            }

            _fontFiles = Directory.GetFiles(fontDir)
                .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                .ToArray();


            // download font previews from our repo if you have yet to generate a mod
            if (_fontFiles.Length == 0)
            {
                try
                {
                    App.Logger?.WriteLine("UI", "No font files found in temp folder, downloading from GitHub...");

                    string[] fontUrls = {
                        "https://raw.githubusercontent.com/RealMeddsam/config/main/BuilderIcons-Regular.ttf",
                        "https://raw.githubusercontent.com/RealMeddsam/config/main/BuilderIcons-Filled.ttf"
                    };

                    using (var httpClient = new HttpClient())
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(30);

                        foreach (var url in fontUrls)
                        {
                            try
                            {
                                string fileName = Path.GetFileName(url);
                                string filePath = Path.Combine(fontDir, fileName);

                                App.Logger?.WriteLine("UI", $"Downloading font: {fileName}");

                                var response = await httpClient.GetAsync(url);
                                response.EnsureSuccessStatusCode();

                                var fontData = await response.Content.ReadAsByteArrayAsync();
                                await File.WriteAllBytesAsync(filePath, fontData);

                                App.Logger?.WriteLine("UI", $"Successfully downloaded: {fileName}");
                            }
                            catch (Exception ex)
                            {
                                App.Logger?.WriteException("UI", ex);
                            }
                        }
                    }

                    _fontFiles = Directory.GetFiles(fontDir)
                        .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                   f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                }
                catch (Exception ex)
                {
                    App.Logger?.WriteException("UI", ex);
                }
            }

            var displayNames = _fontFiles
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Select(f => f.Replace("BuilderIcons-", ""))
                .Distinct()
                .OrderBy(f => f)
                .ToArray();

            FontSelectorComboBox.ItemsSource = displayNames;

            if (_fontFiles.Length > 0 && displayNames.Length > 0)
            {
                FontSelectorComboBox.SelectedIndex = 0;

                string selectedDisplayName = (string)FontSelectorComboBox.SelectedItem;
                string selectedFont = FindFontFile(selectedDisplayName, _fontFiles);

                if (File.Exists(selectedFont))
                {
                    await LoadGlyphPreviewsAsync(selectedFont);
                }
            }
        }

        private string FindFontFile(string displayName, string[] fontFiles)
        {
            string? otfFile = fontFiles.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f) == $"BuilderIcons-{displayName}" &&
                f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));

            if (otfFile != null)
                return otfFile;
            string? ttfFile = fontFiles.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f) == $"BuilderIcons-{displayName}" &&
                f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));

            return ttfFile ?? fontFiles.FirstOrDefault() ?? string.Empty;
        }

        private async void FontSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSelectorComboBox.SelectedIndex < 0 || FontSelectorComboBox.SelectedItem == null)
                return;

            string? selectedDisplayName = FontSelectorComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedDisplayName))
                return;

            string selectedFont = FindFontFile(selectedDisplayName, _fontFiles);

            if (string.IsNullOrEmpty(selectedFont) || !File.Exists(selectedFont))
            {
                App.Logger?.WriteLine("UI", $"Selected font file no longer exists: {selectedFont}");
                return;
            }

            await LoadGlyphPreviewsAsync(selectedFont);
        }

        private async void ModGenerator_Click(object sender, RoutedEventArgs e)
        {
            const string LOG_IDENT = "UI::ModGenerator";
            var overallSw = Stopwatch.StartNew();

            GenerateModButton.IsEnabled = false;
            DownloadStatusText.Text = "Starting mod generation...";
            App.Logger?.WriteLine(LOG_IDENT, "Mod generation started.");

            bool colorCursors = CursorsCheckBox?.IsChecked == true;
            bool colorShiftlock = ShiftlockCheckBox?.IsChecked == true;
            bool colorEmoteWheel = EmoteWheelCheckBox?.IsChecked == true;
            bool colorVoiceChat = VoiceChatCheckBox?.IsChecked == true;
            bool includeModifications = IncludeModificationsCheckBox?.IsChecked == true;
            var solidColor = _solidColor;

            try
            {
                await Task.Run(async () =>
                {
                    void SetStatus(string text) => App.Current.Dispatcher.Invoke(() => DownloadStatusText.Text = text);
                    void Log(string text) => App.Logger?.WriteLine(LOG_IDENT, text);

                    SetStatus("Downloading required packages...");
                    var (luaPackagesZip, extraTexturesZip, contentTexturesZip, versionHash, version) =
                        await ModGenerator.DownloadForModGenerator();

                    Log($"DownloadForModGenerator returned. Version: {version} ({versionHash})");

                    string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");

                    string luaPackagesDir = Path.Combine(froststrapTemp, "ExtraContent", "LuaPackages");
                    string extraTexturesDir = Path.Combine(froststrapTemp, "ExtraContent", "textures");
                    string contentTexturesDir = Path.Combine(froststrapTemp, "content", "textures");

                    void SafeExtract(string zipPath, string targetDir)
                    {
                        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                            return;

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

                        // Fast extraction using SharpZipLib (sequential but handles all compression methods)
                        new FastZip().ExtractZip(zipPath, targetDir, null);
                    }

                    SetStatus("Extracting ZIPs...");
                    Log("Extracting downloaded ZIPs...");

                    Parallel.Invoke(
                        () => SafeExtract(luaPackagesZip, luaPackagesDir),
                        () => SafeExtract(extraTexturesZip, extraTexturesDir),
                        () => SafeExtract(contentTexturesZip, contentTexturesDir)
                    );

                    Log("Extraction complete.");

                    SetStatus("Loading mappings...");
                    Dictionary<string, string[]> mappings = await ModGenerator.LoadMappingsAsync();

                    if (mappings == null || mappings.Count == 0)
                    {
                        throw new Exception("Failed to load mappings. No mappings available.");
                    }

                    Log($"Loaded mappings with {mappings.Count} top-level entries.");

                    SetStatus("Recoloring images...");

                    Log($"Using solid color for recolor: {solidColor}");

                    Log("Starting RecolorAllPngs...");
                    ModGenerator.RecolorAllPngs(froststrapTemp, solidColor, mappings, colorCursors, colorShiftlock, colorEmoteWheel, colorVoiceChat);
                    Log("RecolorAllPngs finished.");

                    try
                    {
                        SetStatus("Recoloring fonts...");
                        Log("Starting font recoloring...");

                        await ModGenerator.RecolorFontsAsync(froststrapTemp, solidColor);
                        Log("Font recoloring finished.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteException(LOG_IDENT, ex);
                        SetStatus("Font recoloring failed but continuing");
                    }

                    SetStatus("Cleaning up unnecessary files...");
                    var preservePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var entry in mappings.Values)
                    {
                        string fullPath = Path.Combine(froststrapTemp, Path.Combine(entry));
                        preservePaths.Add(fullPath);
                    }

                    // this is only needed for preview
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
                                {"Error.png","Error@2x.png","Error@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Blank.png","Blank@2x.png","Blank@3x.png"}
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
                            SolidColor = $"#{solidColor.R:X2}{solidColor.G:X2}{solidColor.B:X2}"
                        }
                    };

                    string infoJson = JsonSerializer.Serialize(infoData, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(infoPath, infoJson);

                    if (includeModifications)
                    {
                        if (!Directory.Exists(Paths.Modifications))
                            Directory.CreateDirectory(Paths.Modifications);

                        int copiedFiles = 0;

                        var itemsToCopy = new List<string>
                                    {
                            Path.Combine(froststrapTemp, "ExtraContent"),
                            Path.Combine(froststrapTemp, "content"),
                            Path.Combine(froststrapTemp, "info.json")
                        };

                        string fontsPath = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

                        foreach (var item in itemsToCopy)
                        {
                            if (!File.Exists(item) && !Directory.Exists(item))
                                continue;

                            string relativePath = Path.GetRelativePath(froststrapTemp, item);
                            string targetPath = Path.Combine(Paths.Modifications, relativePath);

                            if (File.Exists(item))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                                File.Copy(item, targetPath, overwrite: true);
                                copiedFiles++;
                            }
                            else if (Directory.Exists(item))
                            {
                                foreach (var file in Directory.GetFiles(item, "*", SearchOption.AllDirectories))
                                {
                                    if (file.StartsWith(fontsPath, StringComparison.OrdinalIgnoreCase) &&
                                        file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }

                                    string fileRelativePath = Path.GetRelativePath(froststrapTemp, file);
                                    string fileTargetPath = Path.Combine(Paths.Modifications, fileRelativePath);

                                    Directory.CreateDirectory(Path.GetDirectoryName(fileTargetPath)!);
                                    File.Copy(file, fileTargetPath, overwrite: true);
                                    copiedFiles++;
                                }
                            }
                        }

                        SetStatus($"Mod generation completed! Copied {copiedFiles} files to Modifications folder.");
                        Log($"Copied {copiedFiles} files to {Paths.Modifications}");
                    }
                    else
                    {
                        var saveDialog = new SaveFileDialog
                        {
                            FileName = "FroststrapMod.zip",
                            Filter = "ZIP Archives (*.zip)|*.zip",
                            Title = "FroststrapMod"
                        };

                        if (saveDialog.ShowDialog() == true)
                        {
                            using (var zip = new ZipArchive(new FileStream(saveDialog.FileName, FileMode.Create), ZipArchiveMode.Create))
                            {
                                var itemsToInclude = new List<string>
                                {
                                    Path.Combine(froststrapTemp, "ExtraContent"),
                                    Path.Combine(froststrapTemp, "content"),
                                    Path.Combine(froststrapTemp, "info.json")
                                };

                                string fontsPath = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\Font");

                                foreach (var item in itemsToInclude)
                                {
                                    if (!File.Exists(item) && !Directory.Exists(item))
                                        continue;

                                    if (File.Exists(item))
                                    {
                                        string relativePath = Path.GetRelativePath(froststrapTemp, item);
                                        zip.CreateEntryFromFile(item, relativePath);
                                    }
                                    else if (Directory.Exists(item))
                                    {
                                        foreach (var file in Directory.GetFiles(item, "*", SearchOption.AllDirectories))
                                        {
                                            if (file.StartsWith(fontsPath, StringComparison.OrdinalIgnoreCase) &&
                                                file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                                            {
                                                continue;
                                            }

                                            string relativePath = Path.GetRelativePath(froststrapTemp, file);
                                            zip.CreateEntryFromFile(file, relativePath);
                                        }
                                    }
                                }
                            }

                            SetStatus($"Mod generated successfully! Saved to: {saveDialog.FileName}");
                            Log($"Mod zip created at {saveDialog.FileName}");
                        }
                        else
                        {
                            DownloadStatusText.Text = "Save cancelled by user.";
                            Log("User cancelled save dialog.");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                App.Current.Dispatcher.Invoke(() => DownloadStatusText.Text = $"Mod generation failed: {ex.Message}");
                App.Logger?.WriteException(LOG_IDENT, ex);
            }
            finally
            {
                App.Current.Dispatcher.Invoke(() => GenerateModButton.IsEnabled = true);
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

                UpdateGlyphColors();
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

            UpdateGlyphColors();
        }

        private async Task LoadGlyphPreviewsAsync(string fontPath)
        {
            const string LOG_IDENT = "UI::LoadGlyphPreviewAsync";
            App.Logger?.WriteLine(LOG_IDENT, $"Loading glyph previews for font: {fontPath}");

            if (!File.Exists(fontPath))
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Font file does not exist: {fontPath}");
                return;
            }

            try
            {
                var fileInfo = new FileInfo(fontPath);
                if (fileInfo.Length == 0)
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Font file is empty: {fontPath}");
                    return;
                }
            }
            catch
            {
                // If we can't check file info, continue anyway
            }

            var glyphItems = new List<GlyphItem>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount / 4);

            // Create the initial brush
            var colorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                _solidColor.A, _solidColor.R, _solidColor.G, _solidColor.B));
            colorBrush.Freeze();

            try
            {
                GlyphTypeface typeface;
                try
                {
                    typeface = new GlyphTypeface(new Uri(fontPath));
                }
                catch (Exception ex)
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Failed to create GlyphTypeface from {fontPath}: {ex.Message}");
                    return;
                }

                // Get all character codes sorted in DESCENDING order using LINQ
                var characterCodes = typeface.CharacterToGlyphMap.Keys
                    .OrderByDescending(c => c)
                    .ToList();

                // Process in reverse sorted order
                var tasks = characterCodes.Select(async characterCode =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        if (!typeface.CharacterToGlyphMap.TryGetValue(characterCode, out ushort glyphIndex))
                        {
                            return;
                        }

                        Geometry geometry;
                        try
                        {
                            geometry = typeface.GetGlyphOutline(glyphIndex, 40, 40);
                        }
                        catch (Exception ex)
                        {
                            App.Logger?.WriteLine(LOG_IDENT, $"Failed to get glyph outline for character {characterCode}: {ex.Message}");
                            return;
                        }

                        var bounds = geometry.Bounds;
                        var translate = new TranslateTransform(
                            (50 - bounds.Width) / 2 - bounds.X,
                            (50 - bounds.Height) / 2 - bounds.Y
                        );
                        geometry.Transform = translate;

                        var item = new GlyphItem
                        {
                            Data = geometry,
                            ColorBrush = colorBrush
                        };

                        lock (glyphItems)
                        {
                            glyphItems.Add(item);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);

                _glyphItems = glyphItems;

                App.Current.Dispatcher.Invoke(() =>
                {
                    GlyphGrid.ItemsSource = _glyphItems;
                });

                App.Logger?.WriteLine(LOG_IDENT, $"Loaded {_glyphItems.Count} glyph previews.");
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT, ex);

                App.Current.Dispatcher.Invoke(() =>
                {
                    GlyphGrid.ItemsSource = null;
                });
            }
        }

        private void UpdateGlyphColors()
        {
            if (_glyphItems == null || _glyphItems.Count == 0)
                return;

            var newBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                _solidColor.A, _solidColor.R, _solidColor.G, _solidColor.B));
            newBrush.Freeze();

            foreach (var item in _glyphItems)
            {
                item.ColorBrush = newBrush;
            }

            App.Current.Dispatcher.Invoke(() =>
            {
                var collection = GlyphGrid.ItemsSource as ICollectionView;
                if (collection != null)
                {
                    collection.Refresh();
                }
                else
                {
                    GlyphGrid.ItemsSource = null;
                    GlyphGrid.ItemsSource = _glyphItems;
                }
            });
        }
    }
}
