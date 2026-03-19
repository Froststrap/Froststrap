using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.RobloxInterfaces;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.Dialogs
{
    public class ChannelListsViewModel : NotifyPropertyChangedViewModel
    {
        private const string ChannelsJsonUrl = "https://raw.githubusercontent.com/RealMeddsam/config/refs/heads/main/Channels.json";
        private static readonly string CacheFilePath = Path.Combine(Paths.Cache, "ChannelsCache.json");
        private CancellationTokenSource? _cts;

        public ObservableCollection<DeployInfoDisplay> Channels { get; } = new();
        public IAsyncRelayCommand RefreshCommand { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        public ChannelListsViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var cache = await LoadCacheAsync();
            if (cache != null)
            {
                await SyncUIAsync(cache);
                if (cache.Values.FirstOrDefault()?.CachedAt.AddHours(24) < DateTime.UtcNow)
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

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsLoading = true;
            Channels.Clear();

            try
            {
                await Task.Run(async () =>
                {
                    using var client = new HttpClient();
                    var json = await client.GetStringAsync(ChannelsJsonUrl, token);
                    var channelNames = JsonSerializer.Deserialize<string[]>(json);

                    if (channelNames == null || token.IsCancellationRequested) return;

                    var semaphore = new SemaphoreSlim(4);
                    var results = new Dictionary<string, ChannelEntry>();

                    var tasks = channelNames.Select(async name =>
                    {
                        await semaphore.WaitAsync(token);
                        try
                        {
                            var info = await Deployment.GetInfo(name, includeTimestamp: true);

                            var entry = new ChannelEntry
                            {
                                Version = info.Version,
                                VersionGuid = info.VersionGuid,
                                Timestamp = info.Timestamp,
                                CachedAt = DateTime.UtcNow
                            };

                            lock (results) { results[name] = entry; }

                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                if (!token.IsCancellationRequested)
                                {
                                    Channels.Add(new DeployInfoDisplay
                                    {
                                        ChannelName = name,
                                        Version = entry.Version,
                                        VersionGuid = entry.VersionGuid,
                                        Timestamp = entry.Timestamp
                                    });
                                }
                            });
                        }
                        catch { /* Skip failed */ }
                        finally { semaphore.Release(); }
                    });

                    await Task.WhenAll(tasks);

                    if (!token.IsCancellationRequested)
                        await SaveCacheAsync(results);

                }, token);
            }
            catch (OperationCanceledException) { /* Normal exit */ }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SyncUIAsync(Dictionary<string, ChannelEntry> data)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
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

        private async Task SaveCacheAsync(Dictionary<string, ChannelEntry> data)
            => await File.WriteAllTextAsync(CacheFilePath, JsonSerializer.Serialize(data));

        private async Task<Dictionary<string, ChannelEntry>?> LoadCacheAsync()
        {
            if (!File.Exists(CacheFilePath)) return null;
            try
            {
                var json = await File.ReadAllTextAsync(CacheFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, ChannelEntry>>(json);
            }
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