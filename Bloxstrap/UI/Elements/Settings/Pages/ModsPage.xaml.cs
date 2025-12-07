using Bloxstrap.Integrations;
using Bloxstrap.RobloxInterfaces;
using Bloxstrap.UI.ViewModels.Settings;
using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

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
            DataContext = ViewModel;

            IncludeModificationsCheckBox.IsChecked = true;

            // initialize controls state
            SolidColorTextBox.Text = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}";
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
                    await Deployment.DownloadForModGenerator();

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

                try
                {
                    DownloadStatusText.Text = "Recoloring fonts...";
                    App.Logger?.WriteLine(LOG_IDENT, "Starting font recoloring...");

                    await RecolorFontsAsync(froststrapTemp);
                    App.Logger?.WriteLine(LOG_IDENT, "Font recoloring finished.");
                }
                catch (Exception ex)
                {
                    App.Logger?.WriteException(LOG_IDENT, ex);
                    DownloadStatusText.Text += " (Font recoloring failed but continuing)";
                }

                DownloadStatusText.Text = "Cleaning up unnecessary files...";
                var preservePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in mappings.Values)
                {
                    string fullPath = Path.Combine(froststrapTemp, Path.Combine(entry));
                    preservePaths.Add(fullPath);
                }

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
                        if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string relativePath = Path.GetRelativePath(froststrapTemp, file);
                        string destPath = Path.Combine(Paths.Modifications, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        File.Copy(file, destPath, overwrite: true);
                        copiedFiles++;
                    }

                    DownloadStatusText.Text = $"Mod files copied to {Paths.Modifications}";
                    App.Logger?.WriteLine(LOG_IDENT, $"Copied {copiedFiles} files to {Paths.Modifications}");
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
                        ModGenerator.ZipResult(froststrapTemp, saveDialog.FileName);
                        DownloadStatusText.Text = $"Mod generated successfully! Saved to: {saveDialog.FileName}";
                        App.Logger?.WriteLine(LOG_IDENT, $"Mod zip created at {saveDialog.FileName}");
                    }
                    else
                    {
                        DownloadStatusText.Text = "Save cancelled by user.";
                        App.Logger?.WriteLine(LOG_IDENT, "User cancelled save dialog.");
                    }
                }

                overallSw.Stop();
                App.Logger?.WriteLine(LOG_IDENT, $"Mod generation completed successfully in {overallSw.ElapsedMilliseconds} ms.");
            }
            catch (Exception ex)
            {
                DownloadStatusText.Text = $"Error: {ex.Message}";
                App.Logger?.WriteException(LOG_IDENT, ex);
            }
            finally
            {
                GenerateModButton.IsEnabled = true;
            }
        }

        // Update color from textbox (basic, tolerant)
        private void OnSolidColorChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string hex = SolidColorTextBox.Text.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
                // allow 6 or 8 digit hex
                var col = ColorTranslator.FromHtml(hex);
                _solidColor = col;
            }
            catch
            {
                // ignore invalid input until user provides valid value
            }
        }

        private async Task RecolorFontsAsync(string froststrapTemp)
        {
            const string LOG_IDENT = "ModsPage::RecolorFontsAsync";

            try
            {
                DownloadStatusText.Text = "Recoloring fonts...";
                App.Logger?.WriteLine(LOG_IDENT, "Starting font recoloring...");

                string fontSourceDir = Path.Combine(froststrapTemp, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons", "Font");

                if (!Directory.Exists(fontSourceDir))
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Font directory not found: {fontSourceDir}");
                    return;
                }

                var ttfFiles = Directory.GetFiles(fontSourceDir, "*.ttf");
                if (ttfFiles.Length == 0)
                {
                    App.Logger?.WriteLine(LOG_IDENT, "No TTF font files found to recolor.");
                    return;
                }

                App.Logger?.WriteLine(LOG_IDENT, $"Found {ttfFiles.Length} TTF files to recolor");

                string tempScriptPath = ExtractEmbeddedScriptToTemp();
                string hexColorArg = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}".TrimStart('#');

                try
                {
                    string arguments = $"\"{tempScriptPath}\" --path \"{fontSourceDir}\" --color {hexColorArg}";

                    var start = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "py",
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(start);
                    await process!.WaitForExitAsync();

                    string output = await process.StandardOutput.ReadToEndAsync();
                    string errors = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Python script failed with Exit Code {process.ExitCode}. {errors}");
                    }

                    App.Logger?.WriteLine(LOG_IDENT, $"Font recoloring successful: {output.Trim()}");

                    string tempDir = Path.GetDirectoryName(tempScriptPath)!;
                    var strayOtfs = Directory.GetFiles(tempDir, "*.otf");
                    foreach (var strayOtf in strayOtfs)
                    {
                        File.Delete(strayOtf);
                    }
                }
                finally
                {
                    try { if (File.Exists(tempScriptPath)) File.Delete(tempScriptPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT, ex);
                throw;
            }
        }

        private string ExtractEmbeddedScriptToTemp()
        {
            var assembly = Assembly.GetExecutingAssembly();

              string resourceName = "Bloxstrap.Resources.mod_generator.py";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Could not find embedded resource '{resourceName}'. Check the namespace.");

                string tempScriptPath = Path.Combine(Path.GetTempPath(), "Froststrap", $"gen_{Guid.NewGuid()}.py");

                Directory.CreateDirectory(Path.GetDirectoryName(tempScriptPath));

                using (FileStream fileStream = new FileStream(tempScriptPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }

                return tempScriptPath;
            }
        }

        // I hate python so much
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
            string hexColorString = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}";
            SolidColorTextBox.Text = hexColorString;

            await Task.Run(async () =>
            {
                const string LOG_IDENT = "ModsPage::OnChangeSolidColor_Click";
                string stagePath = Path.Combine(Path.GetTempPath(), "Froststrap", "FontStage");
                string tempScriptPath = null!;

                string output = string.Empty;
                string errors = string.Empty;

                try
                {
                    await Dispatcher.InvokeAsync(() => FontRenderStatusText.Text = "Locating and extracting fonts...");

                    string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
                    if (!Directory.Exists(froststrapTemp))
                    {
                        throw new DirectoryNotFoundException($"Froststrap temp folder missing: {froststrapTemp}");
                    }

                    var zips = Directory.GetFiles(froststrapTemp, "extracontent-luapackages-*.zip");
                    if (zips.Length == 0)
                    {
                        throw new FileNotFoundException("No luapackages zip found in temp.");
                    }
                    string chosenZip = zips.OrderByDescending(f => File.GetCreationTimeUtc(f)).First();

                    if (Directory.Exists(stagePath)) Directory.Delete(stagePath, true);
                    Directory.CreateDirectory(stagePath);

                    using (var archive = ZipFile.OpenRead(chosenZip))
                    {
                        var ttfEntries = archive.Entries
                            .Where(en => en.FullName.Replace('\\', '/')
                            .IndexOf("packages/_index/buildericons/buildericons/font/", StringComparison.OrdinalIgnoreCase) >= 0
                                     && en.FullName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));

                        if (!ttfEntries.Any()) throw new Exception("No TTF files found inside the LuaPackages zip.");

                        foreach (var entry in ttfEntries)
                        {
                            string destFile = Path.Combine(stagePath, Path.GetFileName(entry.FullName));
                            entry.ExtractToFile(destFile, true);
                        }
                    }

                    await Dispatcher.InvokeAsync(() => FontRenderStatusText.Text = "Preparing script...");
                    tempScriptPath = ExtractEmbeddedScriptToTemp();

                    await Dispatcher.InvokeAsync(() => FontRenderStatusText.Text = "Running script...");

                    string hexColorArg = hexColorString.TrimStart('#');
                    string arguments = $"\"{tempScriptPath}\" --path \"{stagePath}\" --color {hexColorArg}";

                    // for some reason python is called py on my pc idk why
                    ProcessStartInfo start = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (Process process = Process.Start(start))
                    {
                        process.WaitForExit();

                        output = process.StandardOutput.ReadToEnd();
                        errors = process.StandardError.ReadToEnd();

                        if (process.ExitCode != 0)
                        {
                            string pythonCrashDetails = string.IsNullOrWhiteSpace(errors)
                                ? "No specific error details were captured. Check standard output for clues."
                                : $"Python Traceback: {errors.Trim()}";

                            throw new Exception($"Python script failed with Exit Code {process.ExitCode}. {pythonCrashDetails}");
                        }
                    }

                    await Dispatcher.InvokeAsync(() => FontRenderStatusText.Text = "Moving fonts...");

                    string finalDest = Path.Combine(Paths.Modifications, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons", "Font");
                    Directory.CreateDirectory(finalDest);

                    var convertedFiles = Directory.GetFiles(stagePath, "*.otf");

                    if (convertedFiles.Length == 0)
                    {
                        string pythonErrorDetails = string.IsNullOrWhiteSpace(errors)
                            ? "No specific error details were captured from Python's error stream."
                            : $"Python reported (may not be a full traceback): {errors.Trim()}";

                        throw new Exception(
                            $"CRITICAL FAILURE: Python exited successfully (Code 0), but did not generate the required **.otf** font files in the staging folder: '{stagePath}'. " +
                            $"The script likely failed due to an internal issue. {pythonErrorDetails}"
                        );
                    }

                    foreach (var file in convertedFiles)
                    {
                        File.Copy(file, Path.Combine(finalDest, Path.GetFileName(file)), true);
                    }

                    await Dispatcher.InvokeAsync(() => FontRenderStatusText.Text = $"Success! Colored fonts installed. Output: {output.Trim()}");
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => FontRenderStatusText.Text = $"Error: {ex.Message}");
                    App.Logger?.WriteException(LOG_IDENT, ex);
                }
                finally
                {
                    try { if (Directory.Exists(stagePath)) Directory.Delete(stagePath, true); } catch { /* ignore locks */ }
                    try { if (tempScriptPath != null && File.Exists(tempScriptPath)) File.Delete(tempScriptPath); } catch { /* ignore locks */ }
                }
            });
        }

        // Tries to display font glyphs in preview
        // Doesn't work right now :/

        // lowkey just use old preview sprite sheet
        private async void RenderFromDownloadedZipButton_Click(object sender, RoutedEventArgs e)
        {
            const string LOG_IDENT = "ModsPage::RenderFromDownloadedZipButton_Click";
            try
            {
                FontRenderStatusText.Text = "Locating downloaded luapackages...";
                string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
                if (!Directory.Exists(froststrapTemp))
                {
                    FontRenderStatusText.Text = "No Froststrap temp folder found.";
                    App.Logger?.WriteLine(LOG_IDENT, $"Temp folder missing: {froststrapTemp}");
                    return;
                }

                var zips = Directory.GetFiles(froststrapTemp, "extracontent-luapackages-*.zip");
                if (zips.Length == 0)
                {
                    FontRenderStatusText.Text = "No luapackages zip found in Froststrap temp.";
                    App.Logger?.WriteLine(LOG_IDENT, "No matching zip found.");
                    return;
                }

                string chosenZip = zips.OrderByDescending(f => File.GetCreationTimeUtc(f)).First();
                App.Logger?.WriteLine(LOG_IDENT, $"Using zip: {chosenZip}");
                FontRenderStatusText.Text = "Rendering fonts from downloaded zip...";

                var tempFiles = new List<string>();
                var renderedBitmaps = new List<Bitmap>();

                try
                {
                    using var archive = ZipFile.OpenRead(chosenZip);

                    var ttfEntries = archive.Entries
                        .Where(en => en.FullName.Replace('\\', '/')
                            .IndexOf("Packages/_Index/BuilderIcons/BuilderIcons/Font/", StringComparison.OrdinalIgnoreCase) >= 0
                                     && en.FullName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(en => en.FullName, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (ttfEntries.Count == 0)
                    {
                        FontRenderStatusText.Text = "No BuilderIcons ttf files found inside zip.";
                        App.Logger?.WriteLine(LOG_IDENT, "No matching ttf entries inside zip.");
                        return;
                    }

                    foreach (var entry in ttfEntries)
                    {
                        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(entry.FullName));
                        tempFiles.Add(tempPath);

                        App.Logger?.WriteLine(LOG_IDENT, $"Extracting {entry.FullName} -> {tempPath}");
                        using (var entryStream = entry.Open())
                        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await entryStream.CopyToAsync(fs).ConfigureAwait(false);
                        }

                        try
                        {
                            var bmp = await Task.Run(() => RenderFontGlyphGrid(tempPath, glyphPixelSize: 48, cols: 16, rows: 6, startCodepoint: 0xE000));
                            if (bmp != null) renderedBitmaps.Add(bmp);
                        }
                        catch (Exception ex)
                        {
                            App.Logger?.WriteException(LOG_IDENT, ex);
                        }
                    }

                    if (renderedBitmaps.Count == 0)
                    {
                        FontRenderStatusText.Text = "Failed to render any fonts.";
                        App.Logger?.WriteLine(LOG_IDENT, "Rendered bitmaps list empty after processing entries.");
                        return;
                    }

                    Bitmap finalBitmap;
                    if (renderedBitmaps.Count == 1)
                    {
                        finalBitmap = renderedBitmaps[0];
                    }
                    else
                    {
                        finalBitmap = ComposeVertical(renderedBitmaps[0], renderedBitmaps[1], spacing: 8);

                        renderedBitmaps[0].Dispose();
                        renderedBitmaps[1].Dispose();
                    }

                    var bi = BitmapToImageSource(finalBitmap);
                    await Dispatcher.InvokeAsync(() => FontPreview.Source = bi);

                    finalBitmap.Dispose();

                    FontRenderStatusText.Text = "Font render complete.";
                    App.Logger?.WriteLine(LOG_IDENT, "Font rendering from zip completed.");
                }
                finally
                {
                    foreach (var tf in tempFiles)
                    {
                        try { if (File.Exists(tf)) File.Delete(tf); } catch { /* ignore */ }
                    }

                    foreach (var rb in renderedBitmaps) try { rb.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                FontRenderStatusText.Text = $"Error: {ex.Message}";
                App.Logger?.WriteException("ModsPage::RenderFromDownloadedZipButton_Click", ex);
            }
        }

        private Bitmap RenderFontGlyphGrid(string fontFilePath, int glyphPixelSize = 48, int cols = 16, int rows = 6, int startCodepoint = 0xE000)
        {
            byte[] fontData = File.ReadAllBytes(fontFilePath);
            IntPtr data = Marshal.AllocCoTaskMem(fontData.Length);
            Marshal.Copy(fontData, 0, data, fontData.Length);

            var pfc = new PrivateFontCollection();

            try
            {
                pfc.AddMemoryFont(data, fontData.Length);

                if (pfc.Families.Length == 0)
                {
                    App.Logger?.WriteLine("RenderFontGlyphGrid", "No font families found in file.");
                    return new Bitmap(1, 1);
                }

                var family = pfc.Families[0];

                using var font = new Font(family, glyphPixelSize, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel);
                int cellW = glyphPixelSize * 2;
                int cellH = glyphPixelSize * 2;
                var bmp = new Bitmap(cols * cellW, rows * cellH, PixelFormat.Format32bppArgb);

                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);

                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;

                    using var brush = new SolidBrush(Color.FromArgb(255, _solidColor.R, _solidColor.G, _solidColor.B));
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            int codepoint = startCodepoint + r * cols + c;
                            string text;
                            try
                            {
                                text = char.ConvertFromUtf32(codepoint);
                            }
                            catch
                            {
                                continue;
                            }

                            var rect = new RectangleF(c * cellW, r * cellH, cellW, cellH);
                            g.DrawString(text, font, brush, rect, sf);
                        }
                    }
                }

                return bmp;
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("RenderFontGlyphGrid", ex);
                return new Bitmap(100, 100);
            }
            finally
            {
                pfc.Dispose();
                Marshal.FreeCoTaskMem(data);
            }
        }

        private Bitmap ComposeVertical(Bitmap top, Bitmap bottom, int spacing = 0)
        {
            int width = Math.Max(top.Width, bottom.Width);
            int height = top.Height + spacing + bottom.Height;

            var outBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(outBmp);
            g.Clear(Color.Transparent);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(top, new Rectangle(0, 0, top.Width, top.Height));
            g.DrawImage(bottom, new Rectangle(0, top.Height + spacing, bottom.Width, bottom.Height));
            return outBmp;
        }

        private BitmapImage BitmapToImageSource(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
    }
}