// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Nodes;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Integrations
{
    internal static class ModGenerator
    {
        internal enum Stage
        {
            Downloading = 1,
            Extracting = 2,
            Recoloring = 3,
            Finishing = 4
        }

        internal readonly record struct Progress(Stage Stage, ulong Done, ulong Total)
        {
            public double Fraction => Total == 0 ? 0 : Math.Min(1, (double)Done / Total);
        }

        public static string OutputDirectory => Path.Combine(Path.GetTempPath(), "Froststrap", "ModGenerator");

        private static string CacheDirectory => Path.Combine(Paths.Cache, "ModGenerator");

        internal sealed record Options
        {
            public required string Color { get; init; }
            public double Angle { get; init; }
            public int? Bands { get; init; }
            public string? RobloxIconPath { get; init; }
            public string? SpinnerPath { get; init; }
            public bool SkipPlayIcon { get; init; }
            public bool Cursors { get; init; }
            public bool Shiftlock { get; init; }
            public bool EmoteWheel { get; init; }
        }

        private static JsonObject? RemoteMappings()
        {
            try
            {
                var mappings = App.RemoteData.Prop?.Mappings;
                if (mappings is null || mappings.Count == 0)
                    return null;

                var json = new JsonObject();
                foreach (var (name, parts) in mappings)
                    json[name] = new JsonArray([.. parts.Select(part => (JsonNode?)JsonValue.Create(part))]);
                return json;
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Couldn't read remote mappings, using the built-in set: {ex.Message}");
                return null;
            }
        }

        private static string BuildConfig(Options options)
        {
            bool hasIcon = !string.IsNullOrWhiteSpace(options.RobloxIconPath) && File.Exists(options.RobloxIconPath);

            var config = new JsonObject
            {
                ["output"] = OutputDirectory,
                ["cacheDir"] = CacheDirectory,
                ["domain"] = Deployment.RobloxDomain,
                ["color"] = options.Color,
                ["angle"] = options.Angle,
                ["bands"] = options.Bands is > 0 ? options.Bands : null,
                ["imageMap"] = hasIcon ? new JsonObject { ["tilt"] = options.RobloxIconPath } : new JsonObject(),
                ["skipGlyphs"] = options.SkipPlayIcon ? new JsonArray("play-large", "play-small") : new JsonArray(),
                ["cursors"] = options.Cursors,
                ["shiftlock"] = options.Shiftlock,
                ["emoteWheel"] = options.EmoteWheel,
                ["customLogo"] = string.IsNullOrWhiteSpace(options.RobloxIconPath) ? null : options.RobloxIconPath,
                ["customSpinner"] = string.IsNullOrWhiteSpace(options.SpinnerPath) ? null : options.SpinnerPath,
                ["mappings"] = RemoteMappings(),
                ["generatorVersion"] = App.InternalVersion
            };
            return config.ToJsonString();
        }

        private static readonly string[] LegacyPackagePrefixes = ["extracontent-luapackages-", "extracontent-textures-", "content-textures2-"];
        private static readonly string[] LegacyDirectories = ["ExtraContent", "content", "mod-generator"];

        private static void DeleteLegacyFiles()
        {
            string legacyRoot = Path.Combine(Path.GetTempPath(), "Froststrap");
            try
            {
                if (Directory.Exists(legacyRoot))
                {
                    foreach (string zip in Directory.GetFiles(legacyRoot, "*.zip"))
                    {
                        string name = Path.GetFileName(zip);
                        if (LegacyPackagePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                            File.Delete(zip);
                    }

                    foreach (string dir in LegacyDirectories.Select(d => Path.Combine(legacyRoot, d)))
                    {
                        if (Directory.Exists(dir))
                            Directory.Delete(dir, true);
                    }

                    string legacyInfo = Path.Combine(legacyRoot, "info.json");
                    if (File.Exists(legacyInfo))
                        File.Delete(legacyInfo);
                }

                string legacyVersionCache = Path.Combine(Paths.Cache, "ModGeneratorVersion.json");
                if (File.Exists(legacyVersionCache))
                    File.Delete(legacyVersionCache);
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Couldn't remove old mod generator files: {ex.Message}");
            }
        }

        /// Generates the mod into `OutputDirectory`
        public static Task GenerateAsync(Options options, Action<Progress> onProgress)
        {
            DeleteLegacyFiles();
            string config = BuildConfig(options);
            App.Logger.Info($"Generating mod (color: {options.Color}, angle: {options.Angle}, bands: {options.Bands})");
            return Task.Run(() => Froststrap.Backend.ModGeneratorNative.Generate(config, (stage, done, total) => onProgress(new Progress((Stage)stage, done, total))));
        }

        public static Task ZipAsync(string destination)
            => Task.Run(() => Froststrap.Backend.ModGeneratorNative.Zip(OutputDirectory, destination));
    }
}
