// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

// TODO: Clean up this, possible rip apart code and do
//       class separation using partial implementations

using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace Froststrap.Integrations
{
    internal static class ModGenerator
    {
        private static readonly string[] CursorFiles = ["IBeamCursor.png", "ArrowCursor.png", "ArrowFarCursor.png"];
        private static readonly string[] ShiftlockFiles = ["MouseLockedCursor.png"];
        private static readonly string[] EmoteWheelFiles = ["SelectedGradient.png", "SelectedGradient@2x.png", "SelectedGradient@3x.png", "SelectedLine.png", "SelectedLine@2x.png", "SelectedLine@3x.png"];

        private static readonly HttpClient DownloadClient = new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            CheckCertificateRevocationList = true,
            ServerCertificateCustomValidationCallback = AllowUnknownRevocation
        })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        private static bool AllowUnknownRevocation(HttpRequestMessage request, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            if (errors != SslPolicyErrors.RemoteCertificateChainErrors || chain is null)
                return false;

            return chain.ChainStatus.All(status => status.Status is X509ChainStatusFlags.NoError
                or X509ChainStatusFlags.RevocationStatusUnknown
                or X509ChainStatusFlags.OfflineRevocation);
        }

        public static async Task RecolorTexturesAsync(
            string rootDir,
            string color,
            float angle,
            Dictionary<string, string[]> mappings,
            bool recolorCursors = false,
            bool recolorShiftlock = false,
            bool recolorEmoteWheel = false,
            string? imageSetDataPath = null,
            string? customLogoPath = null,
            string? customSpinnerPath = null)
        {
            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
                return;

            var images = mappings.Values.Select(parts => Path.Combine(rootDir, Path.Combine(parts))).ToList();

            List<(bool Enabled, string[] Files, string RelativeDir)> optionalGroups =
            [
                (recolorCursors, CursorFiles, Path.Combine("content", "textures", "Cursors", "KeyboardMouse")),
                (recolorShiftlock, ShiftlockFiles, Path.Combine("content", "textures")),
                (recolorEmoteWheel, EmoteWheelFiles, Path.Combine("content", "textures", "ui", "Emotes", "Large"))
            ];

            foreach (var (enabled, files, relativeDir) in optionalGroups)
            {
                if (enabled)
                    images.AddRange(files.Select(file => Path.Combine(rootDir, relativeDir, file)));
            }

            string[] existing = [.. images.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase)];

            int failed = await Task.Run(() => Froststrap.Backend.ModGeneratorNative.RecolorImages(existing, color, angle));
            if (failed != 0)
                throw new InvalidOperationException(failed < 0 ? "Failed to recolor images." : $"Failed to recolor {failed} image(s).");

            if (string.IsNullOrWhiteSpace(imageSetDataPath) || !File.Exists(imageSetDataPath))
                return;

            failed = await Task.Run(() => Froststrap.Backend.ModGeneratorNative.RecolorSpriteSheets(imageSetDataPath, color, angle, customLogoPath, customSpinnerPath));
            if (failed != 0)
                throw new InvalidOperationException(failed < 0 ? "Failed to recolor sprite sheets." : $"Failed to recolor {failed} sprite sheet(s).");
        }

        public static async Task RecolorFontsAsync(string froststrapTemp, string color, double angle = 0, string? imageMap = null, int? bands = null, string? skipGlyphs = null)
        {
            string fontDir = Path.Combine(froststrapTemp, "ExtraContent", "LuaPackages", "Packages", "_Index", "BuilderIcons", "BuilderIcons", "Font");
            if (!Directory.Exists(fontDir)) return;

            int angleArg = (int)Math.Round(angle);
            ushort bandsArg = bands is > 0 ? (ushort)Math.Min(bands.Value, ushort.MaxValue) : (ushort)0;

            App.Logger.Info($"Recoloring fonts in {fontDir} (color: {color}, angle: {angleArg}, bands: {bandsArg})");

            int failed = await Task.Run(() => Froststrap.Backend.ModGeneratorNative.RecolorFonts(fontDir, color, angleArg, bandsArg, imageMap, skipGlyphs));

            if (failed != 0)
                throw new InvalidOperationException(failed < 0 ? "Failed to recolor fonts." : $"Failed to recolor {failed} font(s).");
        }

        public static async Task<(string luaZip, string extraZip, string contentZip, string hash, string version)> DownloadForModGenerator(bool overwrite = false)
        {
            Uri clientVersionUrl = UrlBuilder.BuildApiUrl("clientsettingscdn", "v2/client-version/WindowsStudio64", secure: true);
            var clientInfo = await Http.GetJson<ClientVersion>(clientVersionUrl);
            string hash = clientInfo.VersionGuid.Replace("version-", "", StringComparison.Ordinal);
            string tempPath = Path.Combine(Path.GetTempPath(), "Froststrap");
            Directory.CreateDirectory(tempPath);

            async Task<string> DownloadOne(string type)
            {
                string url = $"https://setup.rbxcdn.com/version-{hash}-{type}.zip";
                string path = Path.Combine(tempPath, $"{type}-{hash}.zip");
                if (!overwrite && File.Exists(path) && new FileInfo(path).Length > 0) return path;
                var data = await DownloadClient.GetByteArrayAsync(new Uri(url));
                await File.WriteAllBytesAsync(path, data);
                return path;
            }

            var results = await Task.WhenAll(DownloadOne("extracontent-luapackages"), DownloadOne("extracontent-textures"), DownloadOne("content-textures2"));
            foreach (var file in Directory.GetFiles(tempPath, "*.zip").Where(f => !f.Contains(hash, StringComparison.OrdinalIgnoreCase)))
                try { File.Delete(file); } catch { }
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
            using var stream = Resource.GetStream("Resources/mappings.json");
            if (stream == null) return [];
            return await JsonHelper.DeserializeAsync<Dictionary<string, string[]>>(stream) ?? [];
        }

        public static void ZipResult(string sourceDir, string outputZip)
        {
            if (File.Exists(outputZip)) File.Delete(outputZip);
            ZipFile.CreateFromDirectory(sourceDir, outputZip, CompressionLevel.Optimal, false);
        }
    }
}
