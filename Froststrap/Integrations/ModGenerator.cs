/*
 * Froststrap
 * Copyright (c) Froststrap Team
 *
 * This file is part of Froststrap and is distributed under the terms of the
 * GNU Affero General Public License, version 3 or later.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.IO.Compression;
using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = Avalonia.Media.Color;

namespace Froststrap.Integrations
{
    public static class ModGenerator
    {
        private const string LOG_IDENT = "ModGenerator";

        private const string ModGeneratorVersionApiUrl = "https://api.github.com/repos/Froststrap/mod-generator/releases/latest";
        private static string ModGeneratorVersionCacheFile => Path.Combine(Paths.Cache, "ModGeneratorVersion.json");

        private static readonly string[] CursorFiles = ["IBeamCursor.png", "ArrowCursor.png", "ArrowFarCursor.png"];
        private static readonly string[] ShiftlockFiles = ["MouseLockedCursor.png"];
        private static readonly string[] EmoteWheelFiles = ["SelectedGradient.png", "SelectedGradient@2x.png", "SelectedGradient@3x.png", "SelectedLine.png", "SelectedLine@2x.png", "SelectedLine@3x.png"];

        private static readonly SpriteBlacklist SpriteBlacklistInstance = new()
        {
            Prefixes = ["chat_bubble/", "component_assets/", "icons/controls/voice/", "icons/graphic/", "squircles/"],
            Suffixes = [],
            Keywords = ["goldrobux", "icons/common/play"],
            Strict = ["gradient/gradient_0_100"]
        };

        public record GradientStop(float Stop, Color Color);

        public record SpriteDef(string Name, int X, int Y, int W, int H);

        private class SpriteBlacklist
        {
            public List<string> Prefixes { get; set; } = [];
            public List<string> Suffixes { get; set; } = [];
            public List<string> Keywords { get; set; } = [];
            public List<string> Strict { get; set; } = [];

            public bool IsBlacklisted(string name)
            {
                if (name.StartsWith("icons/graphic/lock", StringComparison.OrdinalIgnoreCase))
                    return false;
                foreach (var p in Prefixes)
                    if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        return true;
                foreach (var s in Suffixes)
                    if (name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                        return true;
                foreach (var k in Keywords)
                    if (name.Contains(k, StringComparison.OrdinalIgnoreCase))
                        return true;
                foreach (var str in Strict)
                    if (string.Equals(name, str, StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }

        private static async Task<GithubRelease?> GetLatestModGeneratorRelease()
        {
            try
            {
                return await Http.GetJson<GithubRelease>(new Uri(ModGeneratorVersionApiUrl));
            }
            catch
            {
                return null;
            }
        }

        private static string GetCachedModGeneratorVersion()
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ModGeneratorVersionCacheFile));
                return doc.RootElement.GetProperty("Version").GetString() ?? "0.0.0";
            }
            catch
            {
                return "0.0.0";
            }
        }

        private static void CacheModGeneratorVersion(string version)
        {
            var state = new { Version = version, UpdatedAt = DateTime.Now };
            File.WriteAllText(ModGeneratorVersionCacheFile, JsonSerializer.Serialize(state));
        }

        public static async Task<string> EnsureModGeneratorAsync()
        {
            string exePath = GetModGeneratorExePath();
            string cachedVersion = GetCachedModGeneratorVersion();

            if (File.Exists(exePath))
            {
                var release = await GetLatestModGeneratorRelease();
                if (release != null)
                {
                    bool needsUpdate = Utilities.CompareVersions(release.TagName, cachedVersion) == VersionComparison.GreaterThan;
                    if (!needsUpdate)
                        return exePath;
                }
            }

            App.Logger.WriteLine(LOG_IDENT, $"Downloading mod-generator (cached: {cachedVersion})...");
            await DownloadModGeneratorInternalAsync();
            return exePath;
        }

        private static string GetModGeneratorExePath()
        {
            string assetName = GetModGeneratorAssetName();
            string cacheDir = Path.Combine(Path.GetTempPath(), "Froststrap", "mod-generator");
            Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, assetName);
        }

        private static async Task DownloadModGeneratorInternalAsync()
        {
            var release = await GetLatestModGeneratorRelease();

            _ = release ?? throw new Exception("Failed to fetch latest mod-generator release.");

            string assetName = GetModGeneratorAssetName();
            var asset = release.Assets?.FirstOrDefault(a => a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));
            string? downloadUrl = asset?.BrowserDownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
                downloadUrl = $"https://github.com/Froststrap/mod-generator/releases/latest/download/{assetName}";

            byte[] data = await App.HttpClient.GetByteArrayAsync(downloadUrl);
            string exePath = GetModGeneratorExePath();
            await File.WriteAllBytesAsync(exePath, data);

            if (!OperatingSystem.IsWindows())
            {
                using var chmod = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{exePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (chmod != null) await chmod.WaitForExitAsync();
            }

            CacheModGeneratorVersion(release.TagName);
            App.Logger.WriteLine(LOG_IDENT, $"mod-generator updated to version {release.TagName}");
        }

        public static void RecolorAllPngs(
            string rootDir,
            Color solidColor,
            Dictionary<string, string[]> mappings,
            bool recolorCursors = false,
            bool recolorShiftlock = false,
            bool recolorEmoteWheel = false,
            List<GradientStop>? gradient = null,
            float gradientAngleDeg = 0f,
            string? getImageSetDataPath = null,
            string? customLogoPath = null,
            string? customSpinnerPath = null)
        {
            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
                return;

            Parallel.ForEach(mappings, kv =>
            {
                string relativePath = Path.Combine(kv.Value);
                string fullPath = Path.Combine(rootDir, relativePath);
                if (File.Exists(fullPath))
                {
                    SafeRecolorImage(fullPath, solidColor, gradient, gradientAngleDeg);
                }
            });

            List<(bool Enabled, string[] Files, string RelativeDir)> optionalGroups =
            [
                (recolorCursors, CursorFiles, Path.Combine("content", "textures", "Cursors", "KeyboardMouse")),
                (recolorShiftlock, ShiftlockFiles, Path.Combine("content", "textures")),
                (recolorEmoteWheel, EmoteWheelFiles, Path.Combine("content", "textures", "ui", "Emotes", "Large"))
            ];

            foreach (var (enabled, files, relativeDir) in optionalGroups)
            {
                if (!enabled) continue;
                foreach (var fileName in files)
                {
                    string targetPath = Path.Combine(rootDir, relativeDir, fileName);
                    if (File.Exists(targetPath))
                        SafeRecolorImage(targetPath, solidColor, gradient, gradientAngleDeg);
                }
            }

            if (!string.IsNullOrWhiteSpace(getImageSetDataPath) && File.Exists(getImageSetDataPath))
            {
                RecolorSpriteSheets(solidColor, gradient, gradientAngleDeg, getImageSetDataPath, customLogoPath, customSpinnerPath);
            }
        }

        private static void RecolorSpriteSheets(
            Color solidColor,
            List<GradientStop>? gradient,
            float gradientAngleDeg,
            string getImageSetDataPath,
            string? customLogoPath,
            string? customSpinnerPath)
        {
            App.Logger?.WriteLine(LOG_IDENT, $"Parsing image set data: {getImageSetDataPath}");

            var spriteData = LuaImageSetParser.Parse(getImageSetDataPath);
            if (spriteData.Count == 0)
            {
                App.Logger?.WriteLine(LOG_IDENT, "No sprite sheets found from Lua file.");
                return;
            }
            App.Logger?.WriteLine(LOG_IDENT, $"Found {spriteData.Count} sprite sheets.");

            foreach (var (sheetPath, sprites) in spriteData)
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Processing sheet: {sheetPath} with {sprites.Count} sprites");
                if (!File.Exists(sheetPath)) continue;

                using var sheet = Image.Load<Rgba32>(sheetPath);
                bool modified = false;

                if (!string.IsNullOrEmpty(customLogoPath) && File.Exists(customLogoPath))
                    modified |= ReplaceCustomSprite(sheet, sprites, "icons/logo/block", customLogoPath);
                if (!string.IsNullOrEmpty(customSpinnerPath) && File.Exists(customSpinnerPath))
                    modified |= ReplaceCustomSprite(sheet, sprites, "icons/graphic/loadingspinner", customSpinnerPath);

                foreach (var sprite in sprites)
                {
                    if (sprite.W <= 0 || sprite.H <= 0) continue;
                    if (SpriteBlacklistInstance.IsBlacklisted(sprite.Name)) continue;

                    if (string.Equals(sprite.Name, "icons/logo/block", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(customLogoPath))
                        continue;
                    if (string.Equals(sprite.Name, "icons/graphic/loadingspinner", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(customSpinnerPath))
                        continue;

                    var rect = new Rectangle(sprite.X, sprite.Y, sprite.W, sprite.H);
                    using var cropped = sheet.Clone(ctx => ctx.Crop(rect));
                    using var recolored = ApplyMaskToImage(cropped, solidColor, gradient, gradientAngleDeg);
                    for (int y = 0; y < sprite.H; y++)
                        for (int x = 0; x < sprite.W; x++)
                            sheet[sprite.X + x, sprite.Y + y] = recolored[x, y];
                    modified = true;
                }

                if (modified)
                {
                    string tempPath = sheetPath + ".tmp";
                    sheet.SaveAsPng(tempPath);
                    ReplaceFileWithRetry(sheetPath, tempPath);
                    App.Logger?.WriteLine(LOG_IDENT, $"Recolored sprite sheet: {sheetPath}");
                }
            }
        }

        private static bool ReplaceCustomSprite(Image<Rgba32> sheet, List<SpriteDef> sprites, string targetSpriteName, string customImagePath)
        {
            var targetSprite = sprites.FirstOrDefault(s => string.Equals(s.Name, targetSpriteName, StringComparison.OrdinalIgnoreCase));
            if (targetSprite == null || targetSprite.W <= 0 || targetSprite.H <= 0)
                return false;

            using var customImage = Image.Load<Rgba32>(customImagePath);
            using var resized = customImage.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(targetSprite.W, targetSprite.H),
                Mode = ResizeMode.Pad,
                PadColor = new Rgba32(0, 0, 0, 0)
            }));

            for (int y = 0; y < targetSprite.H; y++)
                for (int x = 0; x < targetSprite.W; x++)
                {
                    var px = resized[x, y];
                    if (px.A > 0)
                        sheet[targetSprite.X + x, targetSprite.Y + y] = px;
                }
            return true;
        }

        private static void SafeRecolorImage(string path, Color solidColor, List<GradientStop>? gradient, float gradientAngleDeg)
        {
            using var image = Image.Load<Rgba32>(path);
            using var recolored = ApplyMaskToImage(image, solidColor, gradient, gradientAngleDeg);
            string tempPath = path + ".tmp";
            recolored.SaveAsPng(tempPath);
            ReplaceFileWithRetry(path, tempPath);
        }

        private static Image<Rgba32> ApplyMaskToImage(Image<Rgba32> original, Color solidColor, List<GradientStop>? gradient, float gradientAngleDeg)
        {
            var output = original.Clone();
            int width = original.Width;
            int height = original.Height;

            double cos, sin, minProj, maxProj, denom;
            if (gradient != null && gradient.Count > 0)
            {
                double rad = (gradientAngleDeg - 90) * Math.PI / 180.0;
                cos = Math.Cos(rad);
                sin = Math.Sin(rad);
                double w = width - 1;
                double h = height - 1;

                double p00 = 0 * cos + 0 * sin;
                double p10 = w * cos + 0 * sin;
                double p01 = 0 * cos + h * sin;
                double p11 = w * cos + h * sin;
                minProj = Math.Min(Math.Min(p00, p10), Math.Min(p01, p11));
                maxProj = Math.Max(Math.Max(p00, p10), Math.Max(p01, p11));
                denom = maxProj - minProj;
                if (Math.Abs(denom) < 1e-6) denom = 1.0;
            }
            else
            {
                cos = sin = minProj = 0;
                denom = 1.0;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var srcPixel = original[x, y];
                    if (srcPixel.A == 0) continue;

                    Color targetColor;
                    if (gradient != null && gradient.Count > 0)
                    {
                        double proj = x * cos + y * sin;
                        float t = (float)((proj - minProj) / denom);
                        t = Math.Clamp(t, 0f, 1f);
                        targetColor = InterpolateGradient(gradient, t);
                    }
                    else
                    {
                        targetColor = solidColor;
                    }

                    output[x, y] = new Rgba32(targetColor.R, targetColor.G, targetColor.B, srcPixel.A);
                }
            }
            return output;
        }

        private static Color InterpolateGradient(List<GradientStop> stops, float t)
        {
            if (stops == null || stops.Count == 0)
                return Color.FromRgb(255, 255, 255);

            var orderedStops = stops.OrderBy(s => s.Stop).ToList();

            if (t <= orderedStops[0].Stop)
                return orderedStops[0].Color;
            if (t >= orderedStops[^1].Stop)
                return orderedStops[^1].Color;

            GradientStop left = orderedStops[0];
            GradientStop right = orderedStops[^1];
            for (int i = 0; i < orderedStops.Count - 1; i++)
            {
                if (t >= orderedStops[i].Stop && t <= orderedStops[i + 1].Stop)
                {
                    left = orderedStops[i];
                    right = orderedStops[i + 1];
                    break;
                }
            }

            float span = right.Stop - left.Stop;
            float localT = span > 0 ? (t - left.Stop) / span : 0f;
            localT = Math.Clamp(localT, 0f, 1f);

            byte r = (byte)(left.Color.R + (right.Color.R - left.Color.R) * localT);
            byte g = (byte)(left.Color.G + (right.Color.G - left.Color.G) * localT);
            byte b = (byte)(left.Color.B + (right.Color.B - left.Color.B) * localT);
            return Color.FromRgb(r, g, b);
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
                    if (attempts > 5) throw;
                    Thread.Sleep(50);
                }
            }
        }

        private static class LuaImageSetParser
        {
            public static Dictionary<string, List<SpriteDef>> Parse(string luaPath)
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Parsing Lua file: {luaPath}");
                string text = File.ReadAllText(luaPath);
                var result = new Dictionary<string, List<SpriteDef>>();

                var entryRegex = new Regex(
                    @"\['([^']+)'\]\s*=\s*{\s*ImageRectOffset\s*=\s*Vector2\.new\((\d+),\s*(\d+)\)\s*,\s*ImageRectSize\s*=\s*Vector2\.new\((\d+),\s*(\d+)\)\s*,\s*ImageSet\s*=\s*'([^']+)'",
                    RegexOptions.Compiled);

                string[] tableNames = ["assets_1x", "assets_2x", "assets_3x"];
                int totalMatches = 0;

                foreach (string tableName in tableNames)
                {
                    string startPattern = $"function make_{tableName}() {tableName} = {{";
                    int startIdx = text.IndexOf(startPattern);
                    if (startIdx == -1) continue;

                    int braceStart = text.IndexOf('{', startIdx);
                    if (braceStart == -1) continue;
                    int braceEnd = FindMatchingBrace(text, braceStart);
                    string tableContent = text.Substring(braceStart + 1, braceEnd - braceStart - 1);

                    var matches = entryRegex.Matches(tableContent);
                    App.Logger?.WriteLine(LOG_IDENT, $"Found {matches.Count} entries in {tableName}");
                    totalMatches += matches.Count;

                    foreach (Match match in matches)
                    {
                        string name = match.Groups[1].Value;
                        int x = int.Parse(match.Groups[2].Value);
                        int y = int.Parse(match.Groups[3].Value);
                        int w = int.Parse(match.Groups[4].Value);
                        int h = int.Parse(match.Groups[5].Value);
                        string imageSet = match.Groups[6].Value + ".png";

                        string luaDir = Path.GetDirectoryName(luaPath)!;
                        string spriteSheetsDir = Path.Combine(luaDir, "..", "SpriteSheets");
                        string imagePath = Path.GetFullPath(Path.Combine(spriteSheetsDir, imageSet));

                        if (!result.ContainsKey(imagePath))
                            result[imagePath] = [];
                        result[imagePath].Add(new SpriteDef(name, x, y, w, h));
                    }
                }

                App.Logger?.WriteLine(LOG_IDENT, $"Total sprite definitions: {totalMatches}, across {result.Count} sheets");
                if (totalMatches == 0)
                    App.Logger?.WriteLine(LOG_IDENT, "WARNING: No sprites matched. Check the Lua file format.");

                return result;
            }

            private static int FindMatchingBrace(string text, int openBraceIndex)
            {
                int depth = 1;
                for (int i = openBraceIndex + 1; i < text.Length; i++)
                {
                    if (text[i] == '{') depth++;
                    else if (text[i] == '}') depth--;
                    if (depth == 0) return i;
                }
                return -1;
            }
        }

        public static async Task RecolorFontsAsync(string froststrapTemp, Color solidColor, string? gradientStops = null, double? angle = null, string? imageMap = null, int? bands = null, string? skipGlyphs = null)
        {
            string fontDir = Path.Combine(froststrapTemp, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons", "Font");
            if (!Directory.Exists(fontDir)) return;

            string exePath = await EnsureModGeneratorAsync();
            string colorArg = gradientStops ?? $"{solidColor.R:X2}{solidColor.G:X2}{solidColor.B:X2}";
            string args = $"--path \"{fontDir}\" --color {colorArg}";

            if (angle.HasValue) args += $" --angle {angle.Value}";
            if (!string.IsNullOrEmpty(imageMap)) args += $" --image-map \"{imageMap}\"";
            if (bands.HasValue && bands.Value > 0) args += $" --bands {bands.Value}";
            if (!string.IsNullOrEmpty(skipGlyphs)) args += $" --skip-glyphs \"{skipGlyphs}\"";

            App.Logger.WriteLine("RecolorFontsAsync", $"Recolor Args: {args}");
            await ExecuteExeAsync(exePath, args, Path.GetDirectoryName(exePath)!);
        }

        private static string GetModGeneratorAssetName()
        {
            if (OperatingSystem.IsWindows()) return "mod-generator.exe";
            if (OperatingSystem.IsMacOS()) return "mod-generator-macos";
            return "mod-generator-linux";
        }

        public static async Task<(string luaZip, string extraZip, string contentZip, string hash, string version)> DownloadForModGenerator(bool overwrite = false)
        {
            Uri clientVersionUrl = UrlBuilder.BuildApiUrl("clientsettingscdn", "v2/client-version/WindowsStudio64", secure: true);
            var clientInfo = await Http.GetJson<ClientVersion>(clientVersionUrl);
            string hash = clientInfo.VersionGuid.Replace("version-", "");
            string tempPath = Path.Combine(Path.GetTempPath(), "Froststrap");
            Directory.CreateDirectory(tempPath);
            foreach (var file in Directory.GetFiles(tempPath, "*.zip").Where(f => !f.Contains(hash)))
                try { File.Delete(file); } catch { }

            async Task<string> DownloadOne(string type)
            {
                string url = $"https://setup.rbxcdn.com/version-{hash}-{type}.zip";
                string path = Path.Combine(tempPath, $"{type}-{hash}.zip");
                if (!overwrite && File.Exists(path) && new FileInfo(path).Length > 0) return path;
                var data = await App.HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(path, data);
                return path;
            }

            var results = await Task.WhenAll(DownloadOne("extracontent-luapackages"), DownloadOne("extracontent-textures"), DownloadOne("content-textures2"));
            return (results[0], results[1], results[2], hash, clientInfo.Version);
        }

        public static async Task<Dictionary<string, string[]>> LoadMappingsAsync()
        {
            try
            {
                var remoteData = App.RemoteData.Prop;
                if (remoteData?.Mappings?.Count > 0) return remoteData.Mappings;
            }
            catch { }
            return await LoadEmbeddedMappingsAsync();
        }

        private static async Task<Dictionary<string, string[]>> LoadEmbeddedMappingsAsync()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Froststrap.Resources.mappings.json");
            if (stream == null) return [];
            return await JsonSerializer.DeserializeAsync<Dictionary<string, string[]>>(stream) ?? [];
        }

        private static async Task<(int ExitCode, string Output, string Errors)> ExecuteExeAsync(string exe, string args, string workingDir)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await outTask, await errTask);
        }

        public static void ZipResult(string sourceDir, string outputZip)
        {
            if (File.Exists(outputZip)) File.Delete(outputZip);
            ZipFile.CreateFromDirectory(sourceDir, outputZip, CompressionLevel.Optimal, false);
        }
    }
}