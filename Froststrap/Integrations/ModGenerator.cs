/*
 * Froststrap
 * Copyright (c) Froststrap Team
 *
 * This file is part of Froststrap and is distributed under the terms of the
 * GNU Affero General Public License, version 3 or later.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Color = Avalonia.Media.Color;

namespace Froststrap.Integrations
{
    public static class ModGenerator
    {
        private const string LOG_IDENT = "ModGenerator";

        public static void RecolorAllPngs(string rootDir, Color solidColor, Dictionary<string, string[]> mappings, bool recolorCursors = false, bool recolorShiftlock = false, bool recolorEmoteWheel = false)
        {
            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            {
                return;
            }

            Parallel.ForEach(mappings, kv =>
            {
                string relativePath = Path.Combine(kv.Value);
                string fullPath = Path.Combine(rootDir, relativePath);

                if (File.Exists(fullPath))
                {
                    SafeRecolorImage(fullPath, solidColor);
                }
            });

            var optionalGroups = new List<(bool Enabled, string[] Files, string RelativeDir)>
            {
                (recolorCursors, new[] { "IBeamCursor.png", "ArrowCursor.png", "ArrowFarCursor.png" }, Path.Combine("content", "textures", "Cursors", "KeyboardMouse")),
                (recolorShiftlock, new[] { "MouseLockedCursor.png" }, Path.Combine("content", "textures")),
                (recolorEmoteWheel, new[] { "SelectedGradient.png", "SelectedGradient@2x.png", "SelectedGradient@3x.png", "SelectedLine.png", "SelectedLine@2x.png", "SelectedLine@3x.png" }, Path.Combine("content", "textures", "ui", "Emotes", "Large"))
            };

            foreach (var group in optionalGroups.Where(g => g.Enabled))
            {
                foreach (var fileName in group.Files)
                {
                    string targetPath = Path.Combine(rootDir, group.RelativeDir, fileName);

                    if (File.Exists(targetPath))
                    {
                        SafeRecolorImage(targetPath, solidColor);
                    }
                }
            }
        }

        private static void SafeRecolorImage(string path, Color color)
        {
            try
            {
                byte[] imageBytes = File.ReadAllBytes(path);

                using (var image = Image.Load<Rgba32>(imageBytes))
                {
                    image.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < accessor.Height; y++)
                        {
                            Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                            for (int x = 0; x < pixelRow.Length; x++)
                            {
                                ref Rgba32 pixel = ref pixelRow[x];

                                pixel.R = color.R;
                                pixel.G = color.G;
                                pixel.B = color.B;
                            }
                        }
                    });

                    using var ms = new MemoryStream();
                    image.SaveAsPng(ms, new PngEncoder
                    {
                        ColorType = PngColorType.RgbWithAlpha,
                        BitDepth = PngBitDepth.Bit8
                    });

                    File.WriteAllBytes(path, ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException(LOG_IDENT, ex);
            }
        }

        public static async Task RecolorFontsAsync(string froststrapTemp, Color solidColor, string modName)
        {
            string fontDir = Path.Combine(froststrapTemp, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons", "Font");
            if (!Directory.Exists(fontDir)) return;

            string exePath = await DownloadModGeneratorExeAsync();
            string hexColor = $"{solidColor.R:X2}{solidColor.G:X2}{solidColor.B:X2}";

            string args = $"--path \"{fontDir}\" --color {hexColor} --bootstrapper Froststrap --mod-name \"{modName}\"";

            await ExecuteExeAsync(exePath, args, Path.GetDirectoryName(exePath)!);
        }

        private static async Task<string> DownloadModGeneratorExeAsync()
        {
            string cacheDir = Path.Combine(Path.GetTempPath(), "Froststrap", "mod-generator");
            Directory.CreateDirectory(cacheDir);
            string exePath = Path.Combine(cacheDir, "mod-generator.exe");

            if (File.Exists(exePath)) return exePath;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Froststrap/1.0");

            var release = await client.GetFromJsonAsync<GithubRelease>("https://api.github.com/repos/Froststrap/mod-generator/releases/latest");

            string? url = release?.Assets?
                .FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))?
                .BrowserDownloadUrl;

            url ??= "https://github.com/Froststrap/mod-generator/releases/latest/download/mod_generator.exe";

            var data = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(exePath, data);

            return exePath;
        }

        public static async Task<(string luaZip, string extraZip, string contentZip, string hash, string version)> DownloadForModGenerator(bool overwrite = false)
        {
            var clientInfo = await Http.GetJson<ClientVersion>("https://clientsettingscdn.roblox.com/v2/client-version/WindowsStudio64");
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

                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                var data = await client.GetByteArrayAsync(url);
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
            if (stream == null) return new Dictionary<string, string[]>();

            return await JsonSerializer.DeserializeAsync<Dictionary<string, string[]>>(stream) ?? new();
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