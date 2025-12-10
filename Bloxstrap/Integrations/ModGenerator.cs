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

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Reflection;

namespace Bloxstrap.Integrations
{
    public static class ModGenerator
    {
        public static void RecolorAllPngs(string rootDir, Color solidColor, bool recolorCursors = false, bool recolorShiftlock = false, bool recolorEmoteWheel = false, bool recolorVoiceChat = false)
        {
            const string LOG_IDENT = "UI::Recolor";

            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Invalid rootDir '{rootDir}'");
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Bloxstrap.Resources.mappings.json");
            if (stream == null)
            {
                App.Logger?.WriteLine(LOG_IDENT, "mappings.json embedded resource not found");
                return;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var mappings = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
            if (mappings == null || mappings.Count == 0)
            {
                App.Logger?.WriteLine(LOG_IDENT, "mappings.json parsed but empty");
                return;
            }

            App.Logger?.WriteLine(LOG_IDENT, $"Loaded {mappings.Count} valid entries from mappings.json");
            App.Logger?.WriteLine(LOG_IDENT, "RecolorAllPngs started.");

            foreach (var kv in mappings)
            {
                string[] parts = kv.Value;
                string relativePath = Path.Combine(parts);
                string fullPath = Path.Combine(rootDir, relativePath);

                if (!File.Exists(fullPath))
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"File missing: {relativePath}");
                    continue;
                }

                try
                {
                    SafeRecolorImage(fullPath, solidColor);
                }
                catch (Exception ex)
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Error recoloring '{relativePath}': {ex.Message}");
                }
            }

            // Handle additional files based on options
            void TryRecolorFilesByNames(string[] candidateNames, string? relativeDir = null)
            {
                foreach (var name in candidateNames)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(relativeDir))
                        {
                            string expected = Path.Combine(rootDir, relativeDir, name);
                            if (File.Exists(expected))
                            {
                                SafeRecolorImage(expected, solidColor);
                                continue;
                            }
                        }

                        var matches = Directory.EnumerateFiles(rootDir, name, SearchOption.AllDirectories).ToList();
                        if (matches.Count == 0)
                        {
                            matches = Directory.EnumerateFiles(rootDir, "*.png", SearchOption.AllDirectories)
                                .Where(p => p.EndsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
                        }

                        if (matches.Count > 0)
                        {
                            foreach (var m in matches)
                            {
                                try
                                {
                                    SafeRecolorImage(m, solidColor);
                                }
                                catch (Exception ex)
                                {
                                    App.Logger?.WriteLine(LOG_IDENT, $"Error recoloring matched file '{m}': {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteLine(LOG_IDENT, $"Exception while trying to handle '{name}': {ex.Message}");
                    }
                }
            }

            if (recolorCursors)
                TryRecolorFilesByNames(
                    new[] { "IBeamCursor.png", "ArrowCursor.png", "ArrowFarCursor.png" },
                    Path.Combine("content", "textures", "Cursors", "KeyboardMouse"));

            if (recolorShiftlock)
                TryRecolorFilesByNames(
                    new[] { "MouseLockedCursor.png" },
                    Path.Combine("content", "textures"));

            if (recolorEmoteWheel)
                TryRecolorFilesByNames(
                    new[]
                    {
                        "SelectedGradient.png", "SelectedGradient@2x.png", "SelectedGradient@3x.png",
                        "SelectedLine.png", "SelectedLine@2x.png", "SelectedLine@3x.png"
                    },
                    Path.Combine("content", "textures", "ui", "Emotes", "Large"));

            App.Logger?.WriteLine(LOG_IDENT, "RecolorAllPngs finished.");
        }

        public static async Task RecolorFontsAsync(string froststrapTemp, Color solidColor)
        {
            const string LOG_IDENT = "ModGenerator::RecolorFontsAsync";

            try
            {
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
                string hexColorArg = $"#{solidColor.R:X2}{solidColor.G:X2}{solidColor.B:X2}".TrimStart('#');

                try
                {
                    string arguments = $"--path \"{fontSourceDir}\" --color {hexColorArg}";

                    string? workingDirectory = Path.GetDirectoryName(tempScriptPath);
                    if (string.IsNullOrEmpty(workingDirectory))
                    {
                        workingDirectory = Environment.CurrentDirectory;
                        App.Logger?.WriteLine(LOG_IDENT, $"Warning: Could not get directory from tempScriptPath, using current directory: {workingDirectory}");
                    }

                    var result = await PythonLauncher.ExecuteScriptAsync(tempScriptPath, arguments, workingDirectory);

                    if (!string.IsNullOrEmpty(result.Output))
                        App.Logger?.WriteLine(LOG_IDENT, $"Python output: {result.Output}");

                    if (!string.IsNullOrEmpty(result.Errors))
                        App.Logger?.WriteLine(LOG_IDENT, $"Python errors: {result.Errors}");

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Python script failed with Exit Code {result.ExitCode}. {result.Errors}");
                    }

                    App.Logger?.WriteLine(LOG_IDENT, $"Font recoloring successful!");

                    foreach (var ttfFile in ttfFiles)
                    {
                        try
                        {
                            File.Delete(ttfFile);
                            App.Logger?.WriteLine(LOG_IDENT, $"Deleted TTF file: {Path.GetFileName(ttfFile)}");
                        }
                        catch (Exception ex)
                        {
                            App.Logger?.WriteLine(LOG_IDENT, $"Warning: Could not delete TTF file: {ex.Message}");
                        }
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

        private static string ExtractEmbeddedScriptToTemp()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "Bloxstrap.Resources.mod_generator.py";

            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Could not find embedded resource '{resourceName}'. Check the namespace.");

                string tempScriptPath = Path.Combine(Path.GetTempPath(), "Froststrap", $"gen_{Guid.NewGuid()}.py");

                string directoryPath = Path.Combine(Path.GetTempPath(), "Froststrap");
                Directory.CreateDirectory(directoryPath);

                using (FileStream fileStream = new FileStream(tempScriptPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }

                return tempScriptPath;
            }
        }

        public static void UpdateBuilderIconsJson(string froststrapTemp)
        {
            string jsonPath = Path.Combine(froststrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\BuilderIcons\BuilderIcons\BuilderIcons.json");

            if (!File.Exists(jsonPath))
                return;

            try
            {
                string content = File.ReadAllText(jsonPath);
                content = content.Replace(".ttf", ".otf");
                File.WriteAllText(jsonPath, content);
                App.Logger?.WriteLine("ModGenerator", $"Updated BuilderIcons.json: Changed .ttf to .otf");
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModGenerator::UpdateBuilderIconsJson", ex);
            }
        }

        private static void SafeRecolorImage(string path, Color solidColor)
        {
            using (var original = new Bitmap(path))
            using (var recolored = ApplyMask(original, solidColor))
            {
                string tempPath = path + ".tmp";
                recolored.Save(tempPath, ImageFormat.Png);
            }

            ReplaceFileWithRetry(path, path + ".tmp");
        }

        private static Bitmap ApplyMask(Bitmap original, Color solidColor)
        {
            if (original.Width == 0 || original.Height == 0)
                return new Bitmap(original);

            var recolored = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData srcData = original.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = recolored.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                int bytesPerPixel = 4;

                for (int y = 0; y < original.Height; y++)
                {
                    for (int x = 0; x < original.Width; x++)
                    {
                        int idx = y * srcData.Stride + x * bytesPerPixel;
                        byte a = srcPtr[idx + 3];

                        if (a == 0)
                        {
                            dstPtr[idx] = 0;
                            dstPtr[idx + 1] = 0;
                            dstPtr[idx + 2] = 0;
                            dstPtr[idx + 3] = 0;
                            continue;
                        }

                        dstPtr[idx] = solidColor.B;
                        dstPtr[idx + 1] = solidColor.G;
                        dstPtr[idx + 2] = solidColor.R;
                        dstPtr[idx + 3] = a;
                    }
                }
            }

            original.UnlockBits(srcData);
            recolored.UnlockBits(dstData);
            return recolored;
        }

        private static void ReplaceFileWithRetry(string originalPath, string tempPath)
        {
            int attempts = 0;
            while (true)
            {
                try
                {
                    File.Delete(originalPath);
                    File.Move(tempPath, originalPath);
                    break;
                }
                catch (IOException)
                {
                    attempts++;
                    if (attempts > 5)
                        throw;
                    Thread.Sleep(50);
                }
            }
        }

        public static class PythonLauncher
        {
            public static async Task<(int ExitCode, string Output, string Errors)> ExecuteScriptAsync(
                string scriptPath,
                string arguments = "",
                string workingDirectory = "")
            {
                var pythonExecutables = new[] { "python", "python3", "py" };
                Process? process = null;
                Exception? lastException = null;

                foreach (var pythonExe in pythonExecutables)
                {
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = pythonExe,
                            Arguments = $"\"{scriptPath}\" {arguments}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        if (!string.IsNullOrEmpty(workingDirectory))
                            startInfo.WorkingDirectory = workingDirectory;

                        process = Process.Start(startInfo);
                        break;
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
                    {
                        lastException = ex;
                        continue;
                    }
                }

                if (process == null)
                    throw new Exception($"Failed to start Python process. Tried: {string.Join(", ", pythonExecutables)}. Make sure Python is installed.", lastException);

                using (process)
                {
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    await process.WaitForExitAsync();

                    string output = await outputTask;
                    string errors = await errorTask;

                    return (process.ExitCode, output, errors);
                }
            }
        }

        public static async Task<(string luaPackagesZip, string extraTexturesZip, string contentTexturesZip, string versionHash, string version)> DownloadForModGenerator(bool overwrite = false)
        {
            const string LOG_IDENT = "ModGenerator::DownloadForModGenerator";

            try
            {
                string clientJsonUrl = "https://clientsettingscdn.roblox.com/v2/client-version/WindowsStudio64";
                var clientInfo = await Http.GetJson<ClientVersion>(clientJsonUrl);

                if (string.IsNullOrEmpty(clientInfo.VersionGuid) || !clientInfo.VersionGuid.StartsWith("version-"))
                    throw new InvalidHTTPResponseException("Invalid clientVersionUpload from Roblox API.");

                string versionHash = clientInfo.VersionGuid.Substring("version-".Length);
                string version = clientInfo.Version;

                // Base Froststrap temp folder
                string froststrapTemp = Path.Combine(Path.GetTempPath(), "Froststrap");
                Directory.CreateDirectory(froststrapTemp);

                var allZipFiles = Directory.GetFiles(froststrapTemp, "*.zip");
                int deletedCount = 0;

                foreach (var zipFile in allZipFiles)
                {
                    string fileName = Path.GetFileName(zipFile);

                    if (!fileName.Contains(versionHash))
                    {
                        try
                        {
                            File.Delete(zipFile);
                            deletedCount++;
                            App.Logger.WriteLine(LOG_IDENT, $"Deleted old zip file with different hash: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to delete old zip file {fileName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Keeping zip file with current hash: {fileName}");
                    }
                }

                // URLs
                string luaPackagesUrl = $"https://setup.rbxcdn.com/version-{versionHash}-extracontent-luapackages.zip";
                string extraTexturesUrl = $"https://setup.rbxcdn.com/version-{versionHash}-extracontent-textures.zip";
                string contentTexturesUrl = $"https://setup.rbxcdn.com/version-{versionHash}-content-textures2.zip";

                // Download paths
                string luaPackagesZip = Path.Combine(froststrapTemp, $"extracontent-luapackages-{versionHash}.zip");
                string extraTexturesZip = Path.Combine(froststrapTemp, $"extracontent-textures-{versionHash}.zip");
                string contentTexturesZip = Path.Combine(froststrapTemp, $"content-textures2-{versionHash}.zip");

                async Task<string> DownloadFile(string url, string path)
                {
                    if (File.Exists(path))
                    {
                        if (!overwrite)
                        {
                            try
                            {
                                var fi = new FileInfo(path);
                                if (fi.Length > 0)
                                {
                                    App.Logger.WriteLine(LOG_IDENT, $"Zip already exists and will be reused: {path}");
                                    return path;
                                }
                                App.Logger.WriteLine(LOG_IDENT, $"Zip exists but is empty: {path}. Will re-download.");
                                File.Delete(path);
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteException(LOG_IDENT, ex);
                                try { File.Delete(path); } catch { /* ignore */ }
                            }
                        }
                        else
                        {
                            try
                            {
                                App.Logger.WriteLine(LOG_IDENT, $"Overwrite requested — deleting existing zip: {path}");
                                File.Delete(path);
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteException(LOG_IDENT, ex);
                            }
                        }
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Downloading {url} -> {path}");

                    using (var httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(5) })
                    using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await response.Content.CopyToAsync(fs);
                        }
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Saved file to {path}");
                    return path;
                }

                luaPackagesZip = await DownloadFile(luaPackagesUrl, luaPackagesZip);
                extraTexturesZip = await DownloadFile(extraTexturesUrl, extraTexturesZip);
                contentTexturesZip = await DownloadFile(contentTexturesUrl, contentTexturesZip);

                App.Logger.WriteLine(LOG_IDENT, $"Downloaded completed for version {versionHash}. Zip files were not deleted.");

                return (luaPackagesZip, extraTexturesZip, contentTexturesZip, versionHash, version);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                throw;
            }
        }

        public static void ZipResult(string sourceDir, string outputZip)
        {
            if (File.Exists(outputZip))
                File.Delete(outputZip);

            ZipFile.CreateFromDirectory(sourceDir, outputZip, CompressionLevel.Optimal, false);
        }
    }
}