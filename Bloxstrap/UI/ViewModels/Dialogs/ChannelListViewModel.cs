using Bloxstrap.RobloxInterfaces;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Bloxstrap.UI.ViewModels.Dialogs
{
    public class ChannelListsViewModel : NotifyPropertyChangedViewModel
    {
        private const string ChannelsJsonUrl = "https://raw.githubusercontent.com/RealMeddsam/config/refs/heads/main/Channels.json";
        private static readonly string CacheFilePath = Path.Combine(Paths.Cache, "ChannelsCache.json");

        public ObservableCollection<DeployInfoDisplay> Channels { get; } = new();
        public ICommand RefreshCommand { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        public ChannelListsViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var cache = await LoadCacheAsync();
            if (cache != null)
            {
                SyncUI(cache);
                if (DateTime.UtcNow - cache.Values.FirstOrDefault()?.CachedAt > TimeSpan.FromHours(24))
                    await RefreshAsync();
            }
            else
            {
                await RefreshAsync();
            }
        }

        public async Task RefreshAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                using var client = new HttpClient();
                var json = await client.GetStringAsync(ChannelsJsonUrl);
                var channelNames = JsonSerializer.Deserialize<string[]>(json);

                if (channelNames == null) return;

                var semaphore = new SemaphoreSlim(10);
                var results = new Dictionary<string, ChannelEntry>();

                var tasks = channelNames.Select(async name =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var info = await Deployment.GetInfo(name, includeTimestamp: true);
                        lock (results)
                        {
                            results[name] = new ChannelEntry
                            {
                                Version = info.Version,
                                VersionGuid = info.VersionGuid,
                                Timestamp = info.Timestamp,
                                CachedAt = DateTime.UtcNow
                            };
                        }
                    }
                    catch { /* Skip failed channels */ }
                    finally { semaphore.Release(); }
                });

                await Task.WhenAll(tasks);
                SyncUI(results);
                await SaveCacheAsync(results);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void SyncUI(Dictionary<string, ChannelEntry> data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Channels.Clear();
                foreach (var entry in data.OrderBy(x => x.Key))
                {
                    Channels.Add(new DeployInfoDisplay
                    {
                        ChannelName = entry.Key,
                        Version = entry.Value.Version,
                        VersionGuid = entry.Value.VersionGuid,
                        Timestamp = entry.Value.Timestamp
                    });
                }
            });
        }

        private async Task SaveCacheAsync(Dictionary<string, ChannelEntry> data) => await File.WriteAllTextAsync(CacheFilePath, JsonSerializer.Serialize(data));

        private async Task<Dictionary<string, ChannelEntry>?> LoadCacheAsync()
        {
            if (!File.Exists(CacheFilePath)) return null;
            try { return JsonSerializer.Deserialize<Dictionary<string, ChannelEntry>>(await File.ReadAllTextAsync(CacheFilePath)); }
            catch { return null; }
        }

        public class ChannelEntry
        {
            public string Version { get; set; } = "";
            public string VersionGuid { get; set; } = "";
            public DateTime? Timestamp { get; set; }
            public DateTime CachedAt { get; set; }
        }

        public class DeployInfoDisplay
        {
            public string ChannelName { get; set; } = "";
            public string Version { get; set; } = "";
            public string VersionGuid { get; set; } = "";
            public DateTime? Timestamp { get; set; }
            public string DisplayTimestamp => Timestamp?.ToLocalTime().ToString("g") ?? "N/A";
        }
    }
}