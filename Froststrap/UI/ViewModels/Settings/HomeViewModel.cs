// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Froststrap.Integrations.AccountManager;
using Froststrap.Models;
using Froststrap.UI.Elements.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings;

internal class HomeViewModel : NotifyPropertyChangedViewModel, IDisposable, IGameMenuActions
{
    private const int MaxFriends = 200;
    private const int PresenceChunk = 50;
    private const int HeadshotChunk = 100;
    private const int UsersChunk = 200;
    private const int MaxRecent = 20;

    private readonly DispatcherTimer _presenceTimer;
    private readonly Action<AccountManagerAccount?> _onAccountChanged;
    private int _friendsSeq;
    private int _recentSeq;
    private bool _friendsBusy;
    private bool _isJoiningBestRegion;
    private bool _disposed;

    private bool _isFriendsLoading;
    private bool _isRecentLoading;
    private bool _isNewsLoading;
    private int _newsIndex;
    private ObservableCollection<FriendItem> _friends = [];
    private ObservableCollection<QuickPlayGameItem> _recentGames = [];
    private ObservableCollection<NewsCardViewModel> _news = [];

    public ObservableCollection<FriendItem> Friends
    {
        get => _friends;
        private set => SetProperty(ref _friends, value);
    }

    public ObservableCollection<QuickPlayGameItem> RecentGames
    {
        get => _recentGames;
        private set => SetProperty(ref _recentGames, value);
    }

    public ObservableCollection<NewsCardViewModel> News
    {
        get => _news;
        private set => SetProperty(ref _news, value);
    }

    public NewsCardViewModel? SelectedNewsCard =>
        News.Count == 0 ? null : News[Math.Clamp(_newsIndex, 0, News.Count - 1)];

    public bool CanPreviousNews => _newsIndex > 0;
    public bool CanNextNews => News.Count > 0 && _newsIndex < News.Count - 1;

    public bool IsFriendsLoading
    {
        get => _isFriendsLoading;
        private set
        {
            if (SetProperty(ref _isFriendsLoading, value))
                OnPropertyChanged(nameof(ShowFriendsEmpty));
        }
    }

    public bool IsRecentLoading
    {
        get => _isRecentLoading;
        private set
        {
            if (SetProperty(ref _isRecentLoading, value))
                OnPropertyChanged(nameof(ShowRecentEmpty));
        }
    }

    public bool IsNewsLoading
    {
        get => _isNewsLoading;
        private set => SetProperty(ref _isNewsLoading, value);
    }

    public bool HasNews => News.Count > 0;
    public bool ShowNewsEmpty => !IsNewsLoading && !HasNews;

#pragma warning disable CA1822
    public bool IsLoggedIn => AccountManager.Shared?.ActiveAccount != null;
    public bool HasActiveAccount => AccountManager.Shared?.ActiveAccount != null;
#pragma warning restore CA1822
    public bool CanJoinBestRegion => HasActiveAccount && !_isJoiningBestRegion;

    public bool HasFriends => Friends.Count > 0;
    public bool HasRecentGames => RecentGames.Count > 0;
    public bool ShowFriendsEmpty => !IsFriendsLoading && !HasFriends;
    public bool ShowRecentEmpty => !IsRecentLoading && !HasRecentGames;
    public int FriendCount => Friends.Count;
    public int OnlineCount => Friends.Count(f => f.IsOnline);

public ICommand JoinFriendCommand { get; }
    public ICommand ViewFriendProfileCommand { get; }
    public ICommand JoinGameCommand { get; }
    public ICommand VisitPageCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand RejoinLastServerCommand { get; }
    public ICommand JoinBestRegionCommand { get; }
    public ICommand ViewServersCommand { get; }
    public ICommand ViewRobloxServersCommand { get; }
    public ICommand ShowPrivateServersCommand { get; }
    public ICommand ViewSubplacesCommand { get; }
    public ICommand OpenNewsLinkCommand { get; }
    public ICommand PreviousNewsCommand { get; }
    public ICommand NextNewsCommand { get; }
    public ICommand SelectNewsCommand { get; }

    public HomeViewModel()
    {
        JoinFriendCommand = new RelayCommand<FriendItem>(friend =>
        {
            if (friend is { CanJoin: true })
                LaunchRoblox(friend.PlaceId, friend.JobId);
        });

        ViewFriendProfileCommand = new RelayCommand<FriendItem>(friend =>
        {
            if (friend != null)
                Process.Start(new ProcessStartInfo($"https://www.roblox.com/users/{friend.UserId}/profile") { UseShellExecute = true });
        });

        JoinGameCommand = new RelayCommand<QuickPlayGameItem>(item =>
        {
            if (item != null) LaunchRoblox(item.PlaceId, null);
        });

        RejoinLastServerCommand = new RelayCommand<QuickPlayGameItem>(item =>
        {
            if (item != null)
                LaunchRoblox(item.PlaceId, item.LastJobId);
        });

        JoinBestRegionCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null || item.PlaceId == 0 || _isJoiningBestRegion) return;

            _isJoiningBestRegion = true;
            OnPropertyChanged(nameof(CanJoinBestRegion));
            try
            {
                await QuickPlayViewModel.JoinBestRegionCoreAsync(item);
            }
            finally
            {
                _isJoiningBestRegion = false;
                OnPropertyChanged(nameof(CanJoinBestRegion));
            }
        });

        ViewServersCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null) return;
            await QuickPlayViewModel.ShowRobloxServersDialogAsync(item.PlaceId, item.UniverseId, item.OriginalDetails, isTracked: true);
        });

        ViewRobloxServersCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null) return;
            await QuickPlayViewModel.ShowRobloxServersDialogAsync(item.PlaceId, item.UniverseId, item.OriginalDetails, isTracked: false);
        });

        ShowPrivateServersCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null || item.PlaceId == 0) return;
            await QuickPlayViewModel.ShowPrivateServerJoinDialogAsync(item.PlaceId);
        });

        ViewSubplacesCommand = new RelayCommand<QuickPlayGameItem>(async item =>
        {
            if (item == null || item.UniverseId == 0) return;
            await QuickPlayViewModel.ShowSubplaceJoinDialogAsync(item.UniverseId);
        });

        VisitPageCommand = new RelayCommand<QuickPlayGameItem>(item =>
        {
            if (item != null)
                Process.Start(new ProcessStartInfo($"https://www.roblox.com/games/{item.PlaceId}") { UseShellExecute = true });
        });

        OpenNewsLinkCommand = new RelayCommand<string>(link =>
        {
            if (!string.IsNullOrWhiteSpace(link) && link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        });

        PreviousNewsCommand = new RelayCommand(() => SelectNewsAt(_newsIndex - 1));
        NextNewsCommand = new RelayCommand(() => SelectNewsAt(_newsIndex + 1));
        SelectNewsCommand = new RelayCommand<NewsCardViewModel>(card =>
        {
            if (card != null)
                SelectNewsAt(News.IndexOf(card));
        });

        RefreshCommand = new RelayCommand(() =>
        {
            _ = LoadFriendsAsync(showSpinner: true);
            _ = LoadRecentAsync();
            _ = LoadNewsAsync();
        });

        _onAccountChanged = account =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(IsLoggedIn));
                Friends = [];
                RecentGames = [];
                NotifyFriendsChanged();
                NotifyRecentChanged();
                _ = LoadFriendsAsync(showSpinner: true);
                _ = LoadRecentAsync();
            });
        };
        AccountManager.Shared.ActiveAccountChanged += _onAccountChanged;

        _presenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _presenceTimer.Tick += (_, _) => _ = LoadFriendsAsync(showSpinner: false);
        _presenceTimer.Start();

        _ = LoadFriendsAsync(showSpinner: true);
        _ = LoadRecentAsync();
        _ = LoadNewsAsync();
    }

    private void NotifyFriendsChanged()
    {
        OnPropertyChanged(nameof(HasFriends));
        OnPropertyChanged(nameof(ShowFriendsEmpty));
        OnPropertyChanged(nameof(FriendCount));
        OnPropertyChanged(nameof(OnlineCount));
    }

    private void NotifyRecentChanged()
    {
        OnPropertyChanged(nameof(HasRecentGames));
        OnPropertyChanged(nameof(ShowRecentEmpty));
    }

    private async Task LoadFriendsAsync(bool showSpinner)
    {
        if (_friendsBusy) return;
        _friendsBusy = true;
        int seq = Interlocked.Increment(ref _friendsSeq);

        try
        {
            var manager = AccountManager.Shared;
            var account = manager?.ActiveAccount;
            string? cookie = account == null ? null : manager!.GetRoblosecurityForUser(account.UserId);

            if (account == null || string.IsNullOrEmpty(cookie))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Friends = [];
                    NotifyFriendsChanged();
                });
                return;
            }

            if (showSpinner)
                await Dispatcher.UIThread.InvokeAsync(() => IsFriendsLoading = true);

            var friendsUrl = UrlBuilder.BuildApiUrl("friends", $"v1/users/{account.UserId}/friends");
            var friendsResponse = await Http.GetJson<FriendsListResponse>(friendsUrl);
            var friendEntries = friendsResponse?.Data?.Take(MaxFriends).ToList() ?? [];

            if (friendEntries.Count == 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Friends = [];
                    NotifyFriendsChanged();
                });
                return;
            }

            var ids = friendEntries.Select(f => f.Id).ToList();

            var presenceTask = FetchPresenceAsync(ids, cookie);
            var headshotTask = FetchHeadshotsAsync(ids);
            var namesTask = FetchDisplayNamesAsync(ids, cookie);
            var presence = await presenceTask;
            var headshots = await headshotTask;
            var displayNames = await namesTask;

            var items = new List<FriendItem>(friendEntries.Count);
            foreach (var f in friendEntries)
            {
                presence.TryGetValue(f.Id, out var p);
                int type = p?.UserPresenceType ?? 0;
                long placeId = p?.PlaceId ?? p?.RootPlaceId ?? 0;

                displayNames.TryGetValue(f.Id, out var displayName);

                items.Add(new FriendItem
                {
                    UserId = f.Id,
                    Name = f.Name ?? "",
                    DisplayName = string.IsNullOrEmpty(displayName) ? (f.Name ?? "") : displayName,
                    AvatarUrl = headshots.GetValueOrDefault(f.Id, ""),
                    PresenceType = type,
                    GameName = type == 2 ? (p?.LastLocation ?? "") : "",
                    PlaceId = placeId,
                    UniverseId = p?.UniverseId ?? 0,
                    JobId = Guid.TryParse(p?.GameId, out _) ? p!.GameId : null
                });
            }

            var universes = items.Where(i => i.IsInGame && i.UniverseId > 0)
                                 .Select(i => i.UniverseId).Distinct().ToList();
            if (universes.Count > 0)
            {
                try
                {
                    var reqs = universes.Select(u => new ThumbnailRequest
                    {
                        TargetId = (ulong)u,
                        Type = ThumbnailType.GameIcon,
                        Size = ThumbnailSize.Detailed,
                        Format = ThumbnailFormat.Png
                    }).ToList();

                    var urls = await Thumbnails.GetThumbnailUrlsAsync(reqs, CancellationToken.None);
                    var map = new Dictionary<long, string>();
                    for (int i = 0; i < universes.Count; i++)
                    {
                        string? u = urls.ElementAtOrDefault(i);
                        if (!string.IsNullOrEmpty(u)) map[universes[i]] = u;
                    }
                    foreach (var item in items)
                        if (map.TryGetValue(item.UniverseId, out var icon))
                            item.GameIconUrl = icon;
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Home game icon fetch failed: {ex.Message}");
                }
            }

            var sorted = items
                .OrderByDescending(i => i.IsInGame)
                .ThenByDescending(i => i.IsOnline)
                .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (seq != _friendsSeq) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Friends = new ObservableCollection<FriendItem>(sorted);
                NotifyFriendsChanged();
            });
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Home friends load failed: {ex.Message}");
        }
        finally
        {
            _friendsBusy = false;
            await Dispatcher.UIThread.InvokeAsync(() => IsFriendsLoading = false);
        }
    }

    private static async Task<Dictionary<long, UserPresence>> FetchPresenceAsync(List<long> ids, string cookie)
    {
        var result = new Dictionary<long, UserPresence>();

        for (int i = 0; i < ids.Count; i += PresenceChunk)
        {
            var chunk = ids.Skip(i).Take(PresenceChunk).ToList();
            string body = JsonHelper.Serialize(new PresenceBatchRequest { UserIds = [.. chunk] });

            string? json = await PostJsonWithCsrfAsync(
                UrlBuilder.BuildApiUrl("presence", "v1/presence/users").ToString(), body, cookie);
            if (json == null) continue;

            var parsed = JsonHelper.Deserialize<UserPresenceResponse>(json);
            if (parsed?.UserPresences == null) continue;

            foreach (var p in parsed.UserPresences)
                result[p.UserId] = p;
        }

        return result;
    }

    private static async Task<Dictionary<long, string>> FetchHeadshotsAsync(List<long> ids)
    {
        var result = new Dictionary<long, string>();

        for (int i = 0; i < ids.Count; i += HeadshotChunk)
        {
            try
            {
                string joined = string.Join(",", ids.Skip(i).Take(HeadshotChunk));
                var url = UrlBuilder.BuildApiUrl(
                    "thumbnails",
                    $"v1/users/avatar-headshot?userIds={joined}&size=150x150&format=Png&isCircular=false");

                var resp = await Http.GetJson<HeadshotResponse>(url);
                if (resp?.Data == null) continue;

                foreach (var d in resp.Data)
                    if (!string.IsNullOrEmpty(d.ImageUrl))
                        result[d.TargetId] = d.ImageUrl;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Home headshot fetch failed: {ex.Message}");
            }
        }

        return result;
    }

    private static async Task<Dictionary<long, string>> FetchDisplayNamesAsync(List<long> ids, string cookie)
    {
        var result = new Dictionary<long, string>();

        for (int i = 0; i < ids.Count; i += UsersChunk)
        {
            try
            {
                var chunk = ids.Skip(i).Take(UsersChunk).ToList();
                string body = JsonHelper.Serialize(new UserIdsRequest { UserIds = [.. chunk] });

                string? json = await PostJsonWithCsrfAsync(
                    UrlBuilder.BuildApiUrl("users", "v1/users").ToString(), body, cookie);
                if (json == null) continue;

                var parsed = JsonHelper.Deserialize<UsersListResponse>(json);
                if (parsed?.Data == null) continue;

                foreach (var u in parsed.Data)
                    if (!string.IsNullOrEmpty(u.DisplayName))
                        result[u.Id] = u.DisplayName;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Home display name fetch failed: {ex.Message}");
            }
        }

        return result;
    }

    // presence endpoint needs X-CSRF-TOKEN: first POST 403s with token in header, retry with it
    private static async Task<string?> PostJsonWithCsrfAsync(string url, string body, string cookie)
    {
        string? csrf = null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
            if (csrf != null)
                request.Headers.Add("X-CSRF-TOKEN", csrf);

            using var response = await App.HttpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Forbidden && attempt == 0 &&
                response.Headers.TryGetValues("x-csrf-token", out var values))
            {
                csrf = values.FirstOrDefault();
                if (csrf != null) continue;
            }

            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync();
        }

        return null;
    }

    private async Task LoadRecentAsync()
    {
        int seq = Interlocked.Increment(ref _recentSeq);

        try
        {
            if (!IsLoggedIn)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RecentGames = [];
                    NotifyRecentChanged();
                });
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => IsRecentLoading = true);

            var games = await QuickPlayViewModel.FetchRecentlyVisitedFromApiAsync();
            games = [.. games.Take(MaxRecent)];
            await QuickPlayViewModel.EnrichGamesWithDetails(games);

            if (seq != _recentSeq) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RecentGames = new ObservableCollection<QuickPlayGameItem>(games);
                NotifyRecentChanged();
            });
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Home recent games load failed: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsRecentLoading = false);
        }
    }

    private async Task LoadNewsAsync()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsNewsLoading = true);

            var news = await NewsService.GetNewsAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                News = new ObservableCollection<NewsCardViewModel>(news.Select(n => new NewsCardViewModel(n)));
                SelectNewsAt(0);
                OnPropertyChanged(nameof(HasNews));
                OnPropertyChanged(nameof(ShowNewsEmpty));
            });
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Home news load failed: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsNewsLoading = false);
        }
    }

    private void SelectNewsAt(int index)
    {
        if (News.Count == 0) return;

        _newsIndex = Math.Clamp(index, 0, News.Count - 1);
        for (int i = 0; i < News.Count; i++)
            News[i].IsSelected = i == _newsIndex;

        OnPropertyChanged(nameof(SelectedNewsCard));
        OnPropertyChanged(nameof(CanPreviousNews));
        OnPropertyChanged(nameof(CanNextNews));
    }

    private static void LaunchRoblox(long placeId, string? jobId)
    {
        if (placeId == 0) return;

        string deeplink = $"roblox://experiences/start?placeId={placeId}";
        if (!string.IsNullOrEmpty(jobId) && Guid.TryParse(jobId, out _))
            deeplink += "&gameInstanceId=" + Uri.EscapeDataString(jobId.Trim());

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
        {
            _presenceTimer.Stop();
            AccountManager.Shared.ActiveAccountChanged -= _onAccountChanged;
        }

        _disposed = true;
    }
}

internal class NewsCardViewModel : INotifyPropertyChanged
{
    public NewsEntry Entry { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public NewsCardViewModel(NewsEntry entry)
    {
        Entry = entry;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal class FriendItem
{
    public long UserId { get; init; }
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string AvatarUrl { get; init; } = "";

    // 0 offline, 1 online, 2 in game, 3 in studio
    public int PresenceType { get; init; }
    public string GameName { get; init; } = "";
    public long PlaceId { get; init; }
    public long UniverseId { get; init; }
    public string? JobId { get; init; }
    public string GameIconUrl { get; set; } = "";

    public bool IsInGame => PresenceType == 2;
    public bool IsOnline => PresenceType is 1 or 2 or 3;
    public bool IsOnlineNotInGame => PresenceType is 1 or 3;
    public bool CanJoin => IsInGame && PlaceId != 0;

    public string SubText => PresenceType switch
    {
        2 => string.IsNullOrEmpty(GameName) ? "In game" : GameName,
        3 => "In Studio",
        1 => "Online",
        _ => "Offline"
    };

    public string PlayingText => $"{Name} is playing";
}

internal class FriendsListResponse
{
    [JsonPropertyName("data")] public List<FriendEntry>? Data { get; set; }
}

internal class FriendEntry
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
}

internal class HeadshotResponse
{
    [JsonPropertyName("data")] public List<HeadshotEntry>? Data { get; set; }
}

internal class HeadshotEntry
{
    [JsonPropertyName("targetId")] public long TargetId { get; set; }
    [JsonPropertyName("imageUrl")] public string? ImageUrl { get; set; }
}

internal class UserIdsRequest
{
    [JsonPropertyName("userIds")] public List<long> UserIds { get; set; } = [];
    [JsonPropertyName("excludeBannedUsers")] public bool ExcludeBannedUsers { get; set; }
}

internal class UsersListResponse
{
    [JsonPropertyName("data")] public List<FriendEntry>? Data { get; set; }
}
