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
        public static void RecolorAllPngs(string rootDir, Color solidColor, Dictionary<string, string[]> mappings, bool recolorCursors = false, bool recolorShiftlock = false, bool recolorEmoteWheel = false, bool recolorVoiceChat = false)
        {
            const string LOG_IDENT = "UI::Recolor";

            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Invalid rootDir '{rootDir}'");
                return;
            }

            if (mappings == null || mappings.Count == 0)
            {
                App.Logger?.WriteLine(LOG_IDENT, "mappings is null or empty");
                return;
            }

            App.Logger?.WriteLine(LOG_IDENT, $"Loaded {mappings.Count} valid entries from mappings");
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

                // font recoloring requires downloading the exe from our private repo
                string exePath = await DownloadModGeneratorExeAsync();

                string hexColorArg = $"#{solidColor.R:X2}{solidColor.G:X2}{solidColor.B:X2}".TrimStart('#');

                try
                {
                    string arguments = $"--path \"{fontSourceDir}\" --color {hexColorArg} --bootstrapper Froststrap";

                    string workingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;

                    var result = await ExecuteExeAsync(exePath, arguments, workingDirectory);

                    if (!string.IsNullOrEmpty(result.Output))
                        App.Logger?.WriteLine(LOG_IDENT, $"Mod-generator output: {result.Output}");

                    if (!string.IsNullOrEmpty(result.Errors))
                        App.Logger?.WriteLine(LOG_IDENT, $"Mod-generator errors: {result.Errors}");

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Mod-generator failed with Exit Code {result.ExitCode}. {result.Errors}");
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
                catch (Exception ex)
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Error executing mod-generator.exe: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT, ex);
                throw;
            }
        }

        private static async Task<string> DownloadModGeneratorExeAsync()
        {
            const string LOG_IDENT = "ModGenerator::DownloadModGeneratorExeAsync";

            try
            {
                string cacheDir = Path.Combine(Path.GetTempPath(), "Froststrap", "mod-generator");
                Directory.CreateDirectory(cacheDir);

                string exePath = Path.Combine(cacheDir, "mod-generator.exe");

                if (File.Exists(exePath))
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Using cached mod-generator.exe at: {exePath}");
                    return exePath;
                }

                App.Logger?.WriteLine(LOG_IDENT, "Downloading mod-generator.exe from GitHub...");

                string releasesApiUrl = "https://api.github.com/repos/Froststrap/mod-generator/releases/latest";

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Froststrap/1.0");

                    var releaseJson = await httpClient.GetStringAsync(releasesApiUrl);
                    using var releaseDoc = JsonDocument.Parse(releaseJson);
                    var assets = releaseDoc.RootElement.GetProperty("assets");

                    string? downloadUrl = null;

                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";

                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("mod-generator.exe", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("mod-generator-win.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        downloadUrl = "https://github.com/Froststrap/mod-generator/releases/latest/download/mod_generator.exe";
                    }

                    App.Logger?.WriteLine(LOG_IDENT, $"Downloading from: {downloadUrl}");

                    using (var response = await httpClient.GetAsync(downloadUrl))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var fs = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await response.Content.CopyToAsync(fs);
                        }
                    }

                    App.Logger?.WriteLine(LOG_IDENT, $"Downloaded mod-generator.exe to: {exePath}");

                    return exePath;
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT, ex);
                throw new Exception($"Failed to download mod-generator.exe: {ex.Message}");
            }
        }

        private static async Task<(int ExitCode, string Output, string Errors)> ExecuteExeAsync(
            string exePath,
            string arguments = "",
            string workingDirectory = "")
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(workingDirectory))
                startInfo.WorkingDirectory = workingDirectory;

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                    throw new Exception($"Failed to start mod-generator.exe process.");

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                string output = await outputTask;
                string errors = await errorTask;

                return (process.ExitCode, output, errors);
            }
        }

        public static void CleanupModGeneratorCache()
        {
            try
            {
                string cacheDir = Path.Combine(Path.GetTempPath(), "Froststrap", "mod-generator");
                if (Directory.Exists(cacheDir))
                {
                    Directory.Delete(cacheDir, true);
                    App.Logger?.WriteLine("ModGenerator", "Cleaned up mod-generator cache");
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModGenerator::CleanupModGeneratorCache", ex);
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

        public static async Task<Dictionary<string, string[]>> LoadMappingsAsync()
        {
            const string LOG_IDENT = "ModGenerator::LoadMappingsAsync";

            try
            {
                var remoteData = await Task.Run(() => App.RemoteData.Prop);

                if (remoteData?.Mappings != null && remoteData.Mappings.Count > 0)
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Loaded {remoteData.Mappings.Count} mappings from remote data");
                    return remoteData.Mappings;
                }
                else
                {
                    App.Logger?.WriteLine(LOG_IDENT, "No mappings in remote data, falling back to embedded resource");
                    return await LoadEmbeddedMappingsAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Failed to load remote mappings: {ex.Message}");
                return await LoadEmbeddedMappingsAsync();
            }
        }

        private static async Task<Dictionary<string, string[]>> LoadEmbeddedMappingsAsync()
        {
            const string LOG_IDENT = "ModGenerator::LoadEmbeddedMappingsAsync";

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("Bloxstrap.Resources.mappings.json"))
                {
                    if (stream == null)
                    {
                        App.Logger?.WriteLine(LOG_IDENT, "Embedded mappings.json not found");
                        return new Dictionary<string, string[]>();
                    }

                    using (var reader = new StreamReader(stream))
                    {
                        string json = await reader.ReadToEndAsync();
                        var mappings = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);

                        if (mappings != null)
                        {
                            App.Logger?.WriteLine(LOG_IDENT, $"Loaded {mappings.Count} mappings from embedded resource");
                            return mappings;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT, ex);
            }

            return new Dictionary<string, string[]>();
        }

        public static void ZipResult(string sourceDir, string outputZip)
        {
            if (File.Exists(outputZip))
                File.Delete(outputZip);

            ZipFile.CreateFromDirectory(sourceDir, outputZip, CompressionLevel.Optimal, false);
        }
    }
}