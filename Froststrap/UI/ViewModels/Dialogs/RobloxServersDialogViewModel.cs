// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Dialogs;

internal class RobloxServersDialogViewModel : NotifyPropertyChangedViewModel, IDisposable
{
    private readonly long _placeId;
    private readonly long _universeId;
    private readonly bool _isTracked;

    private static readonly JsonSerializerOptions _historyLoadOptions = new() { PropertyNameCaseInsensitive = true };

    private bool _isLoading;
    private bool _isCurrentGameApi;
    private bool _disposed;

    private ObservableCollection<ServerInfo> _servers = [];

    public RobloxServersDialogViewModel(
        long placeId,
        long universeId,
        UniverseDetails? details,
        bool isTracked)
    {
        _placeId = placeId;
        _universeId = universeId;
        _isTracked = isTracked;

        UniverseDetails = details;
        GameName = details?.Data?.Name ?? Localizer.Instance.Get("Menu_QuickPlay_UnknownGame");
        GameThumbnailUrl = details?.Thumbnail?.ImageUrl;
        IsCurrentGameApi = !isTracked;

        RejoinServerCommand = new RelayCommand<object>(OnRejoinServer);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);

        _ = LoadAsync();
    }

    public UniverseDetails? UniverseDetails { get; }

    public string GameName { get; }

    public string? GameThumbnailUrl { get; }

    public bool IsCurrentGameApi
    {
        get => _isCurrentGameApi;
        private set
        {
            if (SetProperty(ref _isCurrentGameApi, value))
                OnPropertyChanged(nameof(IsTrackedGame));
        }
    }

    public bool IsTrackedGame => !IsCurrentGameApi;

    public ObservableCollection<ServerInfo> Servers
    {
        get => _servers;
        private set => SetProperty(ref _servers, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ICommand RejoinServerCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    private async Task LoadAsync()
    {
        if (_disposed || IsLoading) return;

        IsLoading = true;
        try
        {
            if (_isTracked)
                await LoadTrackedServersAsync();
            else
                await LoadApiServersAsync();
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to load servers: {ex.Message}");
        }
        finally
        {
            if (!_disposed)
                IsLoading = false;
        }
    }

    private async Task LoadTrackedServersAsync()
    {
        string cachePath = Path.Combine(Paths.Cache, "GameHistory.json");
        if (!File.Exists(cachePath))
            return;

        var entries = await Task.Run(() =>
        {
            try
            {
                string json = File.ReadAllText(cachePath);
                return JsonHelper.Deserialize<List<GameHistoryEntry>>(json, _historyLoadOptions) ?? [];
            }
            catch
            {
                return [];
            }
        });

        var entry = entries.FirstOrDefault(x => x.UniverseId == _universeId);
        if (entry == null)
            return;

        var sorted = entry.Servers.OrderByDescending(x => x.JoinedAt).ToList();
        foreach (var s in sorted) s.IsLatest = false;
        if (sorted.Count > 0) sorted[0].IsLatest = true;

        await Dispatcher.UIThread.InvokeAsync(() => SetServers(sorted));
    }

    private async Task LoadApiServersAsync()
    {
        using var fetcher = new RobloxServerFetcher();
        var result = await fetcher.FetchServerInstancesAsync(_placeId, maxServers: 15);
        if (result.Servers == null || result.Servers.Count == 0)
            return;

        var servers = new List<ServerInfo>(result.Servers.Count);
        foreach (var s in result.Servers)
        {
            if (string.IsNullOrEmpty(s.Region) ||
                s.Region.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                continue;

            var si = new ServerInfo
            {
                JobId = s.Id,
                Region = s.Region,
                JoinedAt = s.FirstSeen ?? DateTime.UtcNow,
                IsLatest = false,
                Playing = s.Playing,
                MaxPlayers = s.MaxPlayers,
                Uptime = s.UptimeDisplay,
            };

            if (s.PlayerTokens is { Count: > 0 })
                foreach (var t in s.PlayerTokens)
                    si.PlayerTokens.Add(t);

            servers.Add(si);
        }

        await LoadPlayerThumbnailsAsync(servers);

        servers = [.. servers.OrderByDescending(s => s.JoinedAt)];

        await Dispatcher.UIThread.InvokeAsync(() => SetServers(servers));
    }

    private void SetServers(IEnumerable<ServerInfo> servers)
    {
        if (_disposed)
        {
            DisposeServerBitmaps(servers);
            return;
        }

        var oldServers = _servers;
        Servers = new ObservableCollection<ServerInfo>(servers);

        Dispatcher.UIThread.Post(() => DisposeServerBitmaps(oldServers), DispatcherPriority.Background);
    }

    private static void DisposeServerBitmaps(IEnumerable<ServerInfo> servers)
    {
        foreach (var server in servers)
            foreach (var bmp in server.PlayerAvatarThumbnails)
                bmp.Dispose();
    }

    private static async Task LoadPlayerThumbnailsAsync(List<ServerInfo> servers)
    {
        var allTokens = servers
            .SelectMany(s => s.PlayerTokens)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();

        if (allTokens.Count == 0) return;

        const int batchSize = 100;
        var tokenToUrl = new Dictionary<string, string?>();

        var chunks = allTokens
            .Select((t, i) => new { Token = t, Index = i })
            .GroupBy(x => x.Index / batchSize)
            .Select(g => g.Select(x => x.Token).ToList())
            .ToList();

        foreach (var chunk in chunks)
        {
            var requests = chunk.Select(token => new ThumbnailRequest
            {
                Token = token,
                Type = ThumbnailType.AvatarHeadShot,
                Size = ThumbnailSize.Medium,
                Format = ThumbnailFormat.Png,
                IsCircular = true
            }).ToList();

            var urls = await Thumbnails.GetThumbnailUrlsAsync(requests, CancellationToken.None);
            for (int i = 0; i < chunk.Count && i < urls.Length; i++)
                tokenToUrl[chunk[i]] = urls[i];
        }

        using var semaphore = new SemaphoreSlim(15);
        var tasks = servers.Select(async server =>
        {
            await semaphore.WaitAsync();
            try
            {
                var bitmaps = new List<Bitmap>();
                foreach (var playerToken in server.PlayerTokens)
                {
                    if (tokenToUrl.TryGetValue(playerToken, out var url) &&
                        !string.IsNullOrEmpty(url))
                    {
                        try
                        {
                            var bytes = await App.HttpClient.GetByteArrayAsync(new Uri(url));
                            using var ms = new MemoryStream(bytes);
                            bitmaps.Add(Bitmap.DecodeToWidth(ms, 32, BitmapInterpolationMode.LowQuality));
                        }
                        catch { }
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    server.PlayerAvatarThumbnails.Clear();
                    foreach (var bmp in bitmaps)
                        server.PlayerAvatarThumbnails.Add(bmp);

                    int extra = server.Playing - bitmaps.Count;
                    if (extra > 0)
                    {
                        server.ExtraPlayersText = $"+{extra}";
                        server.HasExtraPlayers = true;
                    }
                    else
                    {
                        server.HasExtraPlayers = false;
                    }
                }, DispatcherPriority.Background);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private void OnRejoinServer(object? param)
    {
        if (param is ServerInfo server)
            Launch(_placeId, server.JobId);
    }

    private static void Launch(long placeId, string? jobId = null, string? accessCode = null)
    {
        if (placeId == 0) return;

        string deeplink = $"roblox://experiences/start?placeId={placeId}";

        if (!string.IsNullOrEmpty(accessCode))
            deeplink += "&accessCode=" + Uri.EscapeDataString(accessCode);
        else if (!string.IsNullOrEmpty(jobId))
            deeplink += "&gameInstanceId=" + Uri.EscapeDataString(jobId);

        Process.Start(new ProcessStartInfo(deeplink) { UseShellExecute = true });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
            DisposeServerBitmaps(_servers);

        _disposed = true;
    }
}
