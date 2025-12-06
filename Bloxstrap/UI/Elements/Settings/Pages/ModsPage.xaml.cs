using Bloxstrap.RobloxInterfaces;
using Bloxstrap.UI.ViewModels.Settings;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

            // initialize controls state
            SolidColorTextBox.Text = $"#{_solidColor.R:X2}{_solidColor.G:X2}{_solidColor.B:X2}";
        }

        // Update color from textbox (basic, tolerant)
        private void OnSolidColorChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string hex = SolidColorTextBox.Text.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
                // allow 6 or 8 digit hex
                var col = System.Drawing.ColorTranslator.FromHtml(hex);
                _solidColor = col;
            }
            catch
            {
                // ignore invalid input until user provides valid value
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
                string tempScriptPath = null;

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
                var renderedBitmaps = new List<System.Drawing.Bitmap>();

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

                    System.Drawing.Bitmap finalBitmap;
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

        private System.Drawing.Bitmap RenderFontGlyphGrid(string fontFilePath, int glyphPixelSize = 48, int cols = 16, int rows = 6, int startCodepoint = 0xE000)
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
                    return new System.Drawing.Bitmap(1, 1);
                }

                var family = pfc.Families[0];

                using var font = new System.Drawing.Font(family, glyphPixelSize, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel);
                int cellW = glyphPixelSize * 2;
                int cellH = glyphPixelSize * 2;
                var bmp = new System.Drawing.Bitmap(cols * cellW, rows * cellH, PixelFormat.Format32bppArgb);

                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(System.Drawing.Color.Transparent);

                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;

                    using var brush = new SolidBrush(System.Drawing.Color.FromArgb(255, _solidColor.R, _solidColor.G, _solidColor.B));
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
                return new System.Drawing.Bitmap(100, 100);
            }
            finally
            {
                pfc.Dispose();
                Marshal.FreeCoTaskMem(data);
            }
        }

        private System.Drawing.Bitmap ComposeVertical(System.Drawing.Bitmap top, System.Drawing.Bitmap bottom, int spacing = 0)
        {
            int width = Math.Max(top.Width, bottom.Width);
            int height = top.Height + spacing + bottom.Height;

            var outBmp = new System.Drawing.Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(outBmp);
            g.Clear(System.Drawing.Color.Transparent);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(top, new Rectangle(0, 0, top.Width, top.Height));
            g.DrawImage(bottom, new Rectangle(0, top.Height + spacing, bottom.Width, bottom.Height));
            return outBmp;
        }

        private BitmapImage BitmapToImageSource(System.Drawing.Bitmap bmp)
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