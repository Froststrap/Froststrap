// SPDX-FileCopyrightText: 2026 Froststrap
// SPDX-License-Identifier: MPL-2.0

using System.Net.Http;
using System.Text.Json;

namespace Froststrap.Localisation;

public sealed class RemoteLocalisationService
{
    private const string Hash = "ac54d1f1f43955abb44d1cachmk";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Froststrap", "LocalisationCache");
    private const string ManifestName = "manifest.json";
    private const string CheckName = "last-check.json";

    public static void LoadCached(string cultureName)
    {
        try
        {
            var path = Path.Combine(CacheDir, cultureName + ".json");
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(text))
                    Localizer.Instance.LoadRemoteCultureFromString(cultureName, text);
            }
        }
        catch { }
    }

    public static async Task RefreshAsync(string cultureName, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var manifestUrl = $"https://distributions.crowdin.net/{Hash}/manifest.json";
            using var manifestResp = await Client.GetAsync(manifestUrl, ct);
            if (!manifestResp.IsSuccessStatusCode) return;
            var manifestText = await manifestResp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(manifestText)) return;
            using var manifestDoc = JsonDocument.Parse(manifestText);
            var root = manifestDoc.RootElement;
            if (!root.TryGetProperty("timestamp", out var tsProp) || !root.TryGetProperty("content", out var contentProp)) return;

            var timestamp = tsProp.GetInt64();
            var checkPath = Path.Combine(CacheDir, CheckName);
            long cachedTs = 0;
            if (File.Exists(checkPath))
            {
                try
                {
                    using var checkDoc = JsonDocument.Parse(File.ReadAllText(checkPath));
                    if (checkDoc.RootElement.TryGetProperty("timestamp", out var ctProp)) cachedTs = ctProp.GetInt64();
                }
                catch { }
            }

            var targetPath = FindContentPath(contentProp, cultureName);
            if (string.IsNullOrEmpty(targetPath)) return;

            var cacheFile = Path.Combine(CacheDir, cultureName + ".json");
            bool skip = false;
            if (File.Exists(cacheFile))
            {
                // If manifest timestamp unchanged and file exists, skip download
                if (cachedTs == timestamp) skip = true;
            }

            if (!skip)
            {
                var contentUrl = $"https://distributions.crowdin.net/{Hash}{targetPath}";
                using var contentResp = await Client.GetAsync(contentUrl, ct);
                if (contentResp.IsSuccessStatusCode)
                {
                    var contentText = await contentResp.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(contentText))
                    {
                        // Validate JSON before saving
                        try
                        {
                            using var validate = JsonDocument.Parse(contentText);
                            File.WriteAllText(cacheFile, contentText);
                            File.WriteAllText(checkPath, $"{{\"timestamp\":{timestamp}}}");
                            Localizer.Instance.LoadRemoteCultureFromString(cultureName, contentText);
                        }
                        catch { /* keep existing cache/embedded */ }
                    }
                }
            }
            else
            {
                // Even if skipped, ensure remote tier loaded from cache
                LoadCached(cultureName);
            }
        }
        catch { /* silent */ }
    }

    private static string? FindContentPath(JsonElement contentProp, string cultureName)
    {
        if (contentProp.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in contentProp.EnumerateObject())
        {
            var key = prop.Name;
            // Map locale to Crowdin keys: de-DE matches de entry; pt-BR matches pt-BR; etc.
            // We match if key equals cultureName, or if cultureName starts with key + '-' (e.g. de-DE starts with de-)
            // Also handle exact key match for full locale names in content
            if (key.Equals(cultureName, StringComparison.OrdinalIgnoreCase))
            {
                if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                {
                    var first = prop.Value[0].GetString();
                    if (!string.IsNullOrEmpty(first)) return first;
                }
            }
            else if (cultureName.StartsWith(key + "-", StringComparison.OrdinalIgnoreCase) || cultureName.StartsWith(key, StringComparison.OrdinalIgnoreCase) && cultureName.Length > key.Length)
            {
                // Prefer exact or closest; for simplicity accept first match
                if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                {
                    var first = prop.Value[0].GetString();
                    if (!string.IsNullOrEmpty(first)) return first;
                }
            }
        }
        return null;
    }
}
