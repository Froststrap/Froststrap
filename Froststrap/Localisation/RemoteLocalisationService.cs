// SPDX-FileCopyrightText: 2026 Froststrap
// SPDX-License-Identifier: MPL-2.0

using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Froststrap.Localisation;

public sealed class RemoteLocalisationService
{
    private const string Hash = "ac54d1f1f43955abb44d1cachmk";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static string CacheDir => Path.Combine(Paths.Cache, "LocalisationCache");
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
            if (!root.TryGetProperty("timestamp", out var tsProp)) return;
            if (!root.TryGetProperty("content", out var contentProp)) return;
            var timestamp = tsProp.GetInt64();

            var checkPath = Path.Combine(CacheDir, CheckName);
            var metaPath = Path.Combine(CacheDir, "meta.json");
            long cachedTs = 0;
            if (File.Exists(metaPath))
            {
                try
                {
                    using var metaDoc = JsonDocument.Parse(File.ReadAllText(metaPath));
                    if (metaDoc.RootElement.TryGetProperty("timestamp", out var ts)) cachedTs = ts.GetInt64();
                }
                catch { }
            }

            var targetPath = FindContentPath(contentProp, cultureName);
            if (string.IsNullOrEmpty(targetPath)) return;

            var cacheFile = Path.Combine(CacheDir, cultureName + ".json");
            bool skipDownload = false;
            if (File.Exists(cacheFile) && cachedTs == timestamp)
            {
                skipDownload = true;
            }

            if (!skipDownload)
            {
                var contentUrl = $"https://distributions.crowdin.net/{Hash}{targetPath}";
                using var contentResp = await Client.GetAsync(contentUrl, ct);
                if (contentResp.IsSuccessStatusCode)
                {
                    var contentText = await contentResp.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(contentText))
                    {
                        try
                        {
                            using var validate = JsonDocument.Parse(contentText);
                            var tempFile = cacheFile + ".tmp";
                            File.WriteAllText(tempFile, contentText);
                            File.Move(tempFile, cacheFile, overwrite: true);
                            File.WriteAllText(checkPath, $"{{\"timestamp\":{timestamp},\"locale\":\"{cultureName}\"}}");
                            File.WriteAllText(metaPath, $"{{\"timestamp\":{timestamp}}}");
                            Localizer.Instance.LoadRemoteCultureFromString(cultureName, contentText);
                        }
                        catch
                        {
                            if (File.Exists(cacheFile))
                            {
                                try { Localizer.Instance.LoadRemoteCultureFromString(cultureName, File.ReadAllText(cacheFile)); } catch { }
                            }
                        }
                    }
                }
            }
            else
            {
                LoadCached(cultureName);
            }
        }
        catch { }
    }

    private static string? FindContentPath(JsonElement contentProp, string cultureName)
    {
        if (contentProp.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in contentProp.EnumerateObject())
        {
            var key = prop.Name;
            if (key.Equals(cultureName, StringComparison.OrdinalIgnoreCase))
            {
                if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                {
                    var first = prop.Value[0].GetString();
                    if (!string.IsNullOrEmpty(first)) return first;
                }
            }
            else if (cultureName.Length >= 2 && key.Length == 2 && cultureName.StartsWith(key, StringComparison.OrdinalIgnoreCase) && (cultureName.Length == key.Length + 3 && cultureName[key.Length] == '-'))
            {
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
