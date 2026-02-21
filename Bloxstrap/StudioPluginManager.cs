namespace Bloxstrap
{
    public static class StudioPluginManager
    {
        private const string LOG_IDENT = "StudioIntegration";
        private const string VersionApiUrl = "https://api.github.com/repos/Froststrap/FroststrapStudioRPC/releases/latest";

        private static string PluginFile => Path.Combine(Paths.Roblox, "Plugins", "FroststrapStudioRPC.rbxmx");
        private static string VersionCacheFile => Path.Combine(Paths.Cache, "StudioRPCVersion.json");

        public static void Sync()
        {
            if (!App.Settings.Prop.StudioRPC)
            {
                Uninstall();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var release = await GetLatestRelease();
                    if (release is null) return;

                    string cachedVersion = GetCachedVersion();
                    bool needsUpdate = !File.Exists(PluginFile) ||
                                      !File.Exists(VersionCacheFile) ||
                                      Utilities.CompareVersions(release.TagName, cachedVersion) == VersionComparison.GreaterThan;

                    if (needsUpdate)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Syncing plugin (Local: {cachedVersion}, Remote: {release.TagName})");
                        await DownloadPluginAsync(release);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to sync Studio RPC plugin.");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            });
        }

        private static async Task DownloadPluginAsync(GithubRelease release)
        {
            var asset = release.Assets?.FirstOrDefault(x => x.Name.EndsWith(".rbxmx"));
            if (asset is null) return;

            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", App.ProjectName);

            byte[] data = await client.GetByteArrayAsync(asset.BrowserDownloadUrl);

            Directory.CreateDirectory(Path.GetDirectoryName(PluginFile)!);
            await File.WriteAllBytesAsync(PluginFile, data);

            var state = new { Version = release.TagName, UpdatedAt = DateTime.Now };
            File.WriteAllText(VersionCacheFile, JsonSerializer.Serialize(state));
        }

        private static async Task<GithubRelease?> GetLatestRelease()
        {
            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", App.ProjectName);
                var response = await client.GetStringAsync(VersionApiUrl);
                return JsonSerializer.Deserialize<GithubRelease>(response);
            }
            catch { return null; }
        }

        private static string GetCachedVersion()
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(VersionCacheFile));
                return doc.RootElement.GetProperty("Version").GetString() ?? "0.0.0";
            }
            catch { return "0.0.0"; }
        }

        public static void Uninstall()
        {
            if (File.Exists(PluginFile)) File.Delete(PluginFile);
            if (File.Exists(VersionCacheFile)) File.Delete(VersionCacheFile);
        }
    }
}