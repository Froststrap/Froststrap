// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Models;
using Froststrap.Utility;
using System.Text.Json;

namespace Froststrap.Integrations;

internal static class NewsService
{
    private const string NewsUrl = "https://raw.githubusercontent.com/Froststrap/config/refs/heads/main/News.json";
    private static readonly string CachePath = Path.Combine(Paths.Cache, "News.json");

    public static async Task<List<NewsEntry>> GetNewsAsync()
    {
        try
        {
            var newsFile = await FetchAndParseAsync();
            if (newsFile?.News == null)
                return LoadFromCache();

            if (newsFile.News.Count == 0)
                return [];

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var currentVersion = GetAppVersion();

            var filtered = newsFile.News
                .Where(e => IsValidEntry(e, now, currentVersion))
                .OrderByDescending(e => e.Pinned)
                .ThenByDescending(e => e.Timestamp)
                .ToList();

            return filtered;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"News fetch failed: {ex.Message}");
            return LoadFromCache();
        }
    }

    private static async Task<NewsFile?> FetchAndParseAsync()
    {
        try
        {
            using var response = await App.HttpClient.GetAsync(new Uri(NewsUrl));
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var newsFile = JsonHelper.Deserialize<NewsFile>(json);
            if (newsFile != null)
            {
                await WriteCacheAsync(json);
            }
            return newsFile;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task WriteCacheAsync(string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            await File.WriteAllTextAsync(CachePath, json);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"News cache write failed: {ex.Message}");
        }
    }

    private static List<NewsEntry> LoadFromCache()
    {
        try
        {
            if (!File.Exists(CachePath))
                return [];

            var json = File.ReadAllText(CachePath);
            var newsFile = JsonHelper.Deserialize<NewsFile>(json);
            if (newsFile?.News == null)
                return [];

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var currentVersion = GetAppVersion();

            return newsFile.News
                .Where(e => IsValidEntry(e, now, currentVersion))
                .OrderByDescending(e => e.Pinned)
                .ThenByDescending(e => e.Timestamp)
                .ToList();
        }
        catch (Exception ex)
        {
            App.Logger.Error($"News cache load failed: {ex.Message}");
            return [];
        }
    }

    private static bool IsValidEntry(NewsEntry entry, long now, Version currentVersion)
    {
        if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Title))
            return false;

        if (!entry.Enabled)
            return false;

        if (entry.Expires.HasValue && now > entry.Expires.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(entry.MinVersion) && Version.TryParse(entry.MinVersion, out var minV))
        {
            if (currentVersion < minV)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.MaxVersion) && Version.TryParse(entry.MaxVersion, out var maxV))
        {
            if (currentVersion > maxV)
                return false;
        }

        var platform = OperatingSystem.IsWindows() ? "windows" :
                      OperatingSystem.IsLinux() ? "linux" :
                      OperatingSystem.IsMacOS() ? "macos" : "";

        if (entry.Platforms.Count > 0 && !entry.Platforms.Any(p => p.Equals(platform, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static Version GetAppVersion()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            return ver ?? new Version(0, 0, 0);
        }
        catch
        {
            return new Version(0, 0, 0);
        }
    }
}