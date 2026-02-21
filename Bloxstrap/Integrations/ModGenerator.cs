/*
 * Froststrap
 * Copyright (c) Froststrap Team
 *
 * This file is part of Froststrap and is distributed under the terms of the
 * GNU Affero General Public License, version 3 or later.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace Bloxstrap.Integrations;

public static class ModGenerator
{
    private const string LOG_IDENT = "ModGenerator";

    /// <summary>
    /// Processes and recolors only the PNGs defined in the remote mappings.
    /// </summary>
    public static void RecolorAllPngs(string rootDir, Color solidColor, Dictionary<string, string[]> mappings, bool recolorCursors = false, bool recolorShiftlock = false, bool recolorEmoteWheel = false)
    {
        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
        {
            App.Logger?.WriteLine(LOG_IDENT, $"Invalid rootDir '{rootDir}'");
            return;
        }

        App.Logger?.WriteLine(LOG_IDENT, $"Starting parallel recolor for {mappings.Count} mapping entries.");

        Parallel.ForEach(mappings, kv =>
        {
            string fullPath = Path.Combine(rootDir, Path.Combine(kv.Value));
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

        App.Logger?.WriteLine(LOG_IDENT, "Image recoloring complete.");
    }

    private static void SafeRecolorImage(string path, Color color)
    {
        try
        {
            byte[] processedBytes;
            using (var original = new Bitmap(path))
            using (var recolored = ApplyMask(original, color))
            using (var ms = new MemoryStream())
            {
                recolored.Save(ms, ImageFormat.Png);
                processedBytes = ms.ToArray();
            }

            File.WriteAllBytes(path, processedBytes);
        }
        catch (Exception ex)
        {
            App.Logger?.WriteLine(LOG_IDENT, $"Error processing {Path.GetFileName(path)}: {ex.Message}");
        }
    }

    private static Bitmap ApplyMask(Bitmap source, Color color)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, source.Width, source.Height);

        BitmapData srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* sPtr = (byte*)srcData.Scan0;
            byte* dPtr = (byte*)dstData.Scan0;
            int totalBytes = srcData.Height * srcData.Stride;

            for (int i = 0; i < totalBytes; i += 4)
            {
                dPtr[i] = color.B;      // Blue
                dPtr[i + 1] = color.G;  // Green
                dPtr[i + 2] = color.R;  // Red
                dPtr[i + 3] = sPtr[i + 3]; // Preserve original Alpha
            }
        }

        source.UnlockBits(srcData);
        result.UnlockBits(dstData);
        return result;
    }

    public static async Task RecolorFontsAsync(string froststrapTemp, Color solidColor, string modName)
    {
        string fontDir = Path.Combine(froststrapTemp, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons", "Font");
        if (!Directory.Exists(fontDir)) return;

        string exePath = await DownloadModGeneratorExeAsync();
        string hexColor = $"{solidColor.R:X2}{solidColor.G:X2}{solidColor.B:X2}";

        string args = $"--path \"{fontDir}\" --color {hexColor} --bootstrapper Froststrap --mod-name \"{modName}\"";

        var result = await ExecuteExeAsync(exePath, args, Path.GetDirectoryName(exePath)!);

        if (result.ExitCode != 0)
            App.Logger?.WriteLine(LOG_IDENT, $"Font Tool Error: {result.Errors}");
    }

    private static async Task<string> DownloadModGeneratorExeAsync()
    {
        string cacheDir = Path.Combine(Path.GetTempPath(), "Froststrap", "mod-generator");
        Directory.CreateDirectory(cacheDir);
        string exePath = Path.Combine(cacheDir, "mod-generator.exe");

        if (File.Exists(exePath)) return exePath;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Froststrap/1.4.2");

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

        var tasks = new[] { DownloadOne("extracontent-luapackages"), DownloadOne("extracontent-textures"), DownloadOne("content-textures2") };
        var results = await Task.WhenAll(tasks);

        return (results[0], results[1], results[2], hash, clientInfo.Version);
    }

    public static async Task<Dictionary<string, string[]>> LoadMappingsAsync()
    {
        try
        {
            var remoteData = await Task.Run(() => App.RemoteData.Prop);
            if (remoteData?.Mappings?.Count > 0) return remoteData.Mappings;
        }
        catch { }

        return await LoadEmbeddedMappingsAsync();
    }

    private static async Task<Dictionary<string, string[]>> LoadEmbeddedMappingsAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Bloxstrap.Resources.mappings.json");
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