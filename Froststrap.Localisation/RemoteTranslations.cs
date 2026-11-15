// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json;

namespace Froststrap.Localisation
{
    public static class RemoteTranslations
    {
        private const string DistributionHash = "ac54d1f1f43955abb44d1cachmk";
        private const string DistributionUrl = $"https://distributions.crowdin.net/{DistributionHash}";
        private const string MetaFileName = "meta.json";

        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

        public static int LoadCached(string cacheDirectory)
        {
            string culture = Localizer.CultureName;

            if (culture == Localizer.SourceLocale)
                return 0;

            string path = CachePath(cacheDirectory, culture);

            if (!File.Exists(path))
                return 0;

            return Localizer.ApplyRemote(culture, File.ReadAllText(path));
        }

        public static async Task<int> RefreshAsync(HttpClient client, string cacheDirectory, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(client);

            string culture = Localizer.CultureName;

            if (culture == Localizer.SourceLocale)
                return 0;

            Directory.CreateDirectory(cacheDirectory);

            string cachePath = CachePath(cacheDirectory, culture);
            string metaPath = Path.Combine(cacheDirectory, MetaFileName);
            var meta = ReadMeta(metaPath);

            if (meta.TryGetValue(culture, out var cached)
                && File.Exists(cachePath)
                && DateTimeOffset.UtcNow - cached.Checked < CheckInterval)
                return 0;

            using var manifest = JsonDocument.Parse(await client.GetStringAsync(new Uri($"{DistributionUrl}/manifest.json"), token).ConfigureAwait(false));
            var root = manifest.RootElement;

            long timestamp = root.GetProperty("timestamp").GetInt64();

            if (meta.TryGetValue(culture, out cached) && cached.Timestamp == timestamp && File.Exists(cachePath))
            {
                meta[culture] = (timestamp, DateTimeOffset.UtcNow);
                WriteMeta(metaPath, meta);
                return 0;
            }

            string? contentPath = FindContentPath(root.GetProperty("content"), culture);

            if (contentPath is null)
                return 0;

            string json = await client.GetStringAsync(new Uri(DistributionUrl + contentPath), token).ConfigureAwait(false);

            int applied = Localizer.ApplyRemote(culture, json);

            string tempPath = cachePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, token).ConfigureAwait(false);
            File.Move(tempPath, cachePath, overwrite: true);

            meta[culture] = (timestamp, DateTimeOffset.UtcNow);
            WriteMeta(metaPath, meta);

            return applied;
        }

        private static string CachePath(string cacheDirectory, string culture) => Path.Combine(cacheDirectory, culture + ".json");

        private static string? FindContentPath(JsonElement content, string culture)
        {
            if (content.ValueKind != JsonValueKind.Object)
                return null;

            string language = culture.Split('-')[0];
            string? fallback = null;

            foreach (var property in content.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() == 0)
                    continue;

                string? path = property.Value[0].GetString();

                if (string.IsNullOrEmpty(path))
                    continue;

                if (property.Name.Equals(culture, StringComparison.OrdinalIgnoreCase))
                    return path;

                if (property.Name.Equals(language, StringComparison.OrdinalIgnoreCase))
                    fallback ??= path;
            }

            return fallback;
        }

        private static Dictionary<string, (long Timestamp, DateTimeOffset Checked)> ReadMeta(string path)
        {
            var result = new Dictionary<string, (long, DateTimeOffset)>(StringComparer.Ordinal);

            if (!File.Exists(path))
                return result;

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    long timestamp = property.Value.GetProperty("timestamp").GetInt64();
                    long checkedAt = property.Value.GetProperty("checked").GetInt64();
                    result[property.Name] = (timestamp, DateTimeOffset.FromUnixTimeSeconds(checkedAt));
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                result.Clear();
            }

            return result;
        }

        private static void WriteMeta(string path, Dictionary<string, (long Timestamp, DateTimeOffset Checked)> meta)
        {
            using var stream = File.Create(path);
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();

            foreach (var (culture, entry) in meta)
            {
                writer.WriteStartObject(culture);
                writer.WriteNumber("timestamp", entry.Timestamp);
                writer.WriteNumber("checked", entry.Checked.ToUnixTimeSeconds());
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
    }
}
