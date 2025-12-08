/*
 *  Froststrap
 *  Copyright (c) Froststrap Team
 *
 *  This file is part of Froststrap and is distributed under the terms of the
 *  GNU Affero General Public License, version 3 or later.
 *
 *  SPDX-License-Identifier: AGPL-3.0-or-later
 *
 *  Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
 *               of the Nix ecosystem. 
 */

using Bloxstrap.Integrations;
using Bloxstrap.UI.Elements.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public record AccountPresence(int UserPresenceType, string LastLocation, string StatusColor, string ToolTipText);
    public record PlaceDetails(string name, string builder, bool hasVerifiedBadge, long universeId);
    public record PlaceInfo(long Id, long UniverseId, string Name, string? ThumbnailUrl);
    public record Account(long Id, string DisplayName, string Username, string? AvatarUrl);
    public record RecentGameInfo(long UniverseId, long RootPlaceId, string Name, int? Playing, long? Visits, string ThumbnailUrl);
    public record FriendInfo(long Id, string DisplayName, string? AvatarUrl, int PresenceType, string LastLocation, string StatusColor, string PlayingGameName)
    {
        public bool IsOnline => PresenceType == 2;
    }
    public record PrivateServerInfo(long VipServerId, string AccessCode, string Name, long OwnerId, string OwnerName, string? OwnerAvatarUrl, int MaxPlayers, int CurrentPlayers);

    public partial class AccountManagerViewModel : ObservableObject
    {
        private const string LOG_IDENT = "AccountManagerViewModel";

        [ObservableProperty]
        private string _currentUserDisplayName = "Not Logged In";

        [ObservableProperty]
        private string _currentUserUsername = "";

        [ObservableProperty]
        private string _currentUserAvatarUrl = "";

        [ObservableProperty]
        private BitmapImage? _currentUserAvatar;

        [ObservableProperty]
        private bool _isChangeAccountDialogOpen;

        [ObservableProperty]
        private ObservableCollection<Account> _accounts = new();

        [ObservableProperty]
        private Account? _selectedAccount;

        [ObservableProperty]
        private string _placeId = "";

        [ObservableProperty]
        private string _searchQuery = "";

        [ObservableProperty]
        private Account? _draggedAccount;

        [ObservableProperty]
        private ObservableCollection<GameSearchResult> _searchResults = new();

        [ObservableProperty]
        private GameSearchResult? _selectedSearchResult;

        [ObservableProperty]
        private string _serverId = "";

        [ObservableProperty]
        private ObservableCollection<RecentGameInfo> _DiscoveryGames = new();

        [ObservableProperty]
        private ObservableCollection<RecentGameInfo> _continuePlayingGames = new();

        [ObservableProperty]
        private bool _isLoadingContinuePlaying = false;

        [ObservableProperty]
        private bool _isAutoSearching;

        [ObservableProperty]
        private string _autoSearchStatus = "";

        [ObservableProperty]
        private ObservableCollection<PlaceInfo> _subplaces = new();

        [ObservableProperty]
        private bool _isLoadingSubplaces = false;

        [ObservableProperty]
        private ObservableCollection<string> _regions = new();

        [ObservableProperty]
        private string? _selectedRegion;

        [ObservableProperty]
        private int _selectedSortOrder = 2;

        [ObservableProperty]
        private string? _selectedGameThumbnail;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _isDataLoaded = true;

        [ObservableProperty]
        private ObservableCollection<RecentGameInfo> _favoriteGames = new();

        [ObservableProperty]
        private bool _isLoadingFavorites = false;

        [ObservableProperty]
        private string _selectedGameName = "";

        [ObservableProperty]
        private string _selectedGameCreator = "";

        [ObservableProperty]
        private bool _isSelectedGameCreatorVerified = false;

        [ObservableProperty]
        private long? _selectedGameVisits = 0;

        [ObservableProperty]
        private int? _selectedGamePlaying = 0;

        [ObservableProperty]
        private ObservableCollection<string> _friendFilters = new(new[] { "All", "Studio", "Online", "Website", "Offline" });

        [ObservableProperty]
        private string _selectedFriendFilter = "All";

        [ObservableProperty]
        private ObservableCollection<FriendInfo> _filteredFriends = new();

        [ObservableProperty]
        private AccountPresence? _currentUserPresence;

        [ObservableProperty]
        private bool _isPresenceLoading = false;

        [ObservableProperty]
        private string _currentUserPlayingGame = "";

        [ObservableProperty]
        private bool _isPlayingGame;

        [ObservableProperty]
        private int _friendsCount;

        [ObservableProperty]
        private int _followersCount;

        [ObservableProperty]
        private int _followingCount;

        [ObservableProperty]
        private bool _isAccountInformationVisible;

        [ObservableProperty]
        private string _presenceStatus = "";

        [ObservableProperty]
        private ObservableCollection<string> _addMethods = new(new[] { "Quick Sign-In", "Browser" });

        [ObservableProperty]
        private string _selectedAddMethod = "Quick Sign-In";

        [ObservableProperty]
        private ICommand? _autoFindAndJoinSelectedCommand;

        [ObservableProperty]
        private ObservableCollection<FriendInfo> _friends = new();

        [ObservableProperty]
        private bool _isPrivateServersModalOpen;

        [ObservableProperty]
        private ObservableCollection<PrivateServerInfo> _privateServers = new();

        [ObservableProperty]
        private bool _arePrivateServersEmpty;

        [ObservableProperty]
        private bool _isAccountManagerV2Enabled;

        public bool HasAccounts => Accounts.Any();
        public bool HasActiveAccount => GetManager()?.ActiveAccount != null;
        public bool ShouldShowGames => HasAccounts && HasActiveAccount;
        public bool HasSubplaces => Subplaces.Any();

        public List<SortOrderComboBoxItem> SortOrderOptions => new()
        {
            new SortOrderComboBoxItem { Content = "Large Servers", Tag =2 },
            new SortOrderComboBoxItem { Content = "Small Servers", Tag =1 }
        };

        private class FriendData
        {
            public long Id { get; set; }
            public string DisplayName { get; set; } = "";
            public bool IsOnline { get; set; }
            public int PresenceType { get; set; }
        }

        private CancellationTokenSource? _searchDebounceCts;
        private System.Timers.Timer? _presenceUpdateTimer;
        private static readonly HttpClient _http = new();
        private AccountManager? _AccountManager;
        private ActivityWatcher? _activityWatcher;

        partial void OnIsAccountManagerV2EnabledChanged(bool value)
        {
            try
            {
                if (!value)
                    return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (Window w in Application.Current.Windows)
                    {
                        if (w is Bloxstrap.UI.Elements.AccountManagers.MainWindow existing)
                        {
                            if (!existing.IsVisible)
                                existing.Show();
                            existing.Activate();
                            return;
                        }
                    }

                    var wnd = new Bloxstrap.UI.Elements.AccountManagers.MainWindow();
                    wnd.Owner = Application.Current.MainWindow;
                    wnd.Show();
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::OnIsAccountManagerV2EnabledChanged", $"Exception: {ex.Message}");
            }
        }

        public void OpenAccountManagerWindowIfEnabled()
        {
            try
            {
                if (!IsAccountManagerV2Enabled)
                    return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (Window w in Application.Current.Windows)
                    {
                        if (w is Bloxstrap.UI.Elements.AccountManagers.MainWindow existing)
                        {
                            if (!existing.IsVisible) existing.Show();
                            existing.Activate();
                            return;
                        }
                    }

                    var wnd = new Bloxstrap.UI.Elements.AccountManagers.MainWindow();
                    wnd.Owner = Application.Current.MainWindow;
                    wnd.Show();
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::OpenAccountManagerWindowIfEnabled", $"Exception: {ex.Message}");
            }
        }

        private AccountManager? GetManager()
        {
            if (_AccountManager != null)
                return _AccountManager;

            try
            {
                _AccountManager = new AccountManager();

                _AccountManager.NoAccountsFound += () =>
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        IsChangeAccountDialogOpen = true;
                    });
                };
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::GetManager", $"Exception: {ex.Message}");
            }
            return _AccountManager;
        }

        public AccountManagerViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            AutoFindAndJoinSelectedCommand = new RelayCommand(async () => await AutoFindAndJoinSelectedGameAsync());

            _activityWatcher = new ActivityWatcher();
            _activityWatcher.OnHistoryUpdated += (_, _) => _ = RefreshContinuePlaying();

            _ = InitializeDataAsync();

            InitializePresenceTimer();
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                await LoadDataAsync();
                await LoadRegionsAsync();

                App.Logger.WriteLine("AccountManagerViewModel::InitializeDataAsync", "Loaded normally");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountManagerViewModel::InitializeDataAsync", $"Exception: {ex.Message}");
                CurrentUserDisplayName = "Error Loading";
                CurrentUserUsername = "Failed to load account data";
            }
        }

        private async Task LoadDataAsync()
        {
            const string LOG_IDENT_LOAD_DATA = $"{LOG_IDENT}::LoadData";

            Accounts.Clear();

            var mgr = GetManager();
            if (mgr == null)
            {
                CurrentUserDisplayName = "Not Available";
                CurrentUserUsername = "Account manager unavailable";
                CurrentUserAvatarUrl = "";
                IsAccountInformationVisible = false;
                return;
            }

            PlaceId = mgr.CurrentPlaceId ?? "";
            ServerId = mgr.CurrentServerInstanceId ?? "";

            SelectedRegion = mgr.SelectedRegion;

            var accountTasks = mgr.Accounts.Select(async acc =>
            {
                string? avatarUrl = await GetAvatarUrl(acc.UserId);
                return new Account(acc.UserId, acc.DisplayName, acc.Username, string.IsNullOrEmpty(avatarUrl) ? null : avatarUrl);
            }).ToList();

            var accountResults = await Task.WhenAll(accountTasks);
            foreach (var account in accountResults)
            {
                Accounts.Add(account);
            }

            if (mgr.ActiveAccount is not null)
            {
                CurrentUserDisplayName = mgr.ActiveAccount.DisplayName;
                CurrentUserUsername = $"@{mgr.ActiveAccount.Username}";

                string? avatarUrl = await GetAvatarUrl(mgr.ActiveAccount.UserId);
                CurrentUserAvatarUrl = avatarUrl ?? "";

                await LoadAvatarImage(mgr.ActiveAccount.UserId);

                SelectedAccount = Accounts.FirstOrDefault(a => a.Id == mgr.ActiveAccount.UserId);

                _ = UpdateAccountInformationAsync(mgr.ActiveAccount.UserId);

                _ = RefreshFriends();

                _ = RefreshDiscoveryGames();
                await RefreshFavoriteGames();
                await RefreshContinuePlaying();
            }
            else
            {
                CurrentUserDisplayName = "Not Logged In";
                CurrentUserUsername = "";
                CurrentUserAvatarUrl = "";
                CurrentUserAvatar = null;
                IsAccountInformationVisible = false;
            }

            if (!string.IsNullOrEmpty(PlaceId) && long.TryParse(PlaceId, out long currentPlaceId))
            {
                _ = LoadGameThumbnailAsync(currentPlaceId);
                _ = LoadSubplacesForSelectedGameAsync();
            }
        }

        private async Task AutoFindAndJoinSelectedGameAsync()
        {
            const string LOG_IDENT_AUTO_JOIN_SELECTED = $"{LOG_IDENT}::AutoFindAndJoinSelectedGame";

            if (SelectedSearchResult == null)
            {
                if (string.IsNullOrWhiteSpace(PlaceId) || !long.TryParse(PlaceId, out long placeId) || placeId == 0)
                {
                    Frontend.ShowMessageBox("Please select a game first or enter a valid Place ID.", MessageBoxImage.Warning);
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_AUTO_JOIN_SELECTED, $"Using PlaceId from input field: {PlaceId}");
                await AutoFindAndJoinGameAsync(placeId);
                return;
            }

            App.Logger.WriteLine(LOG_IDENT_AUTO_JOIN_SELECTED, $"Using selected game: {SelectedSearchResult.Name} (PlaceId: {SelectedSearchResult.RootPlaceId})");
            await AutoFindAndJoinGameAsync(SelectedSearchResult.RootPlaceId);
        }

        private void InitializePresenceTimer()
        {
            _presenceUpdateTimer = new System.Timers.Timer(20000); // 20 seconds
            _presenceUpdateTimer.Elapsed += async (sender, e) =>
            {
                try
                {
                    await CheckPresenceAsync();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine($"{LOG_IDENT}::PresenceTimer", $"Exception: {ex.Message}");
                }
            };
            _presenceUpdateTimer.AutoReset = true;
            _presenceUpdateTimer.Start();

            _ = CheckPresenceAsync();
        }

        private async Task<BitmapImage?> LoadBitmapFromUrlAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return null;

            const int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var client = new HttpClient();
                    var imageData = await client.GetByteArrayAsync(imageUrl);

                    return await Task.Run(() =>
                    {
                        try
                        {
                            using var stream = new MemoryStream(imageData);
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = stream;
                            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            return bitmap;
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine($"LoadBitmapFromUrlAsync::BitmapCreation (Attempt {attempt})", $"Exception: {ex.Message}");
                            return null;
                        }
                    });
                }
                catch (HttpRequestException ex)
                {
                    App.Logger.WriteLine($"LoadBitmapFromUrlAsync::NetworkError (Attempt {attempt})", $"HTTP error: {ex.Message}");

                    if (attempt == maxRetries)
                    {
                        App.Logger.WriteLine("LoadBitmapFromUrlAsync::FinalAttempt", $"Final attempt failed: {ex.Message}");
                        return null;
                    }

                    await Task.Delay(500 * attempt);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine($"LoadBitmapFromUrlAsync::OtherError (Attempt {attempt})", $"Exception: {ex.Message}");

                    if (attempt == maxRetries)
                        return null;

                    await Task.Delay(500 * attempt);
                }
            }

            return null;
        }

        private async Task LoadAvatarForActiveAccountAsync(long userId)
        {
            const string LOG_IDENT_AVATAR = $"{LOG_IDENT}::LoadAvatarForActiveAccount";

            try
            {
                string? avatarUrl = await GetAvatarUrl(userId);
                CurrentUserAvatarUrl = avatarUrl ?? "";

                if (avatarUrl != null)
                {
                    var bitmap = await LoadBitmapFromUrlAsync(avatarUrl);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentUserAvatar = bitmap;
                    });
                }
                else
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentUserAvatar = null;
                    });
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_AVATAR, $"Exception: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentUserAvatar = null;
                });
            }
        }

        private async Task UpdateAccountAvatarsAsync()
        {
            const string LOG_IDENT_AVATARS = $"{LOG_IDENT}::UpdateAccountAvatars";

            if (!Accounts.Any())
                return;

            try
            {
                var userIds = Accounts.Select(a => a.Id).ToList();
                var avatarUrls = await FetchAvatarsConcurrentlyAsync(userIds, CancellationToken.None);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    for (int i = 0; i < Accounts.Count; i++)
                    {
                        var account = Accounts[i];
                        if (avatarUrls.TryGetValue(account.Id, out var avatarUrl))
                        {
                            var updatedAccount = new Account(account.Id, account.DisplayName, account.Username, avatarUrl ?? null);
                            Accounts[i] = updatedAccount;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_AVATARS, $"Exception: {ex.Message}");
            }
        }

        private async Task<Dictionary<long, string?>> FetchAvatarsConcurrentlyAsync(IEnumerable<long> ids, CancellationToken token = default)
        {
            var result = new Dictionary<long, string?>();
            var sem = new SemaphoreSlim(8);
            var tasks = new List<Task>();

            foreach (var id in ids)
            {
                if (token.IsCancellationRequested)
                {
                    result[id] = null;
                    continue;
                }

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await sem.WaitAsync(token).ConfigureAwait(false);
                        string? url = null;
                        try
                        {
                            url = await GetAvatarUrl(id).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine($"{LOG_IDENT}::FetchAvatarsConcurrently", $"Failed to fetch avatar for {id}: {ex.Message}");
                        }

                        lock (result)
                        {
                            result[id] = url;
                        }
                    }
                    finally
                    {
                        sem.Release();
                    }
                }, token));
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }

            foreach (var id in ids)
            {
                if (!result.ContainsKey(id))
                    result[id] = null;
            }

            return result;
        }

        private async Task<string?> GetAvatarUrl(long userId)
        {
            const string LOG_IDENT_AVATAR_URL = $"{LOG_IDENT}::GetAvatarUrl";

            if (userId == 0)
                return null;

            try
            {
                string url = $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=75x75&format=Png&isCircular=true";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);

                var mgr = GetManager();
                if (mgr?.ActiveAccount != null)
                {
                    string? cookie = mgr.GetRoblosecurityForUser(mgr.ActiveAccount.UserId);
                    if (!string.IsNullOrEmpty(cookie))
                    {
                        req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
                    }
                }

                using var resp = await _http.SendAsync(req).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_AVATAR_URL, $"Thumbnail request failed for {userId}: {(int)resp.StatusCode}");
                    return null;
                }

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in dataElem.EnumerateArray())
                        {
                            if (item.TryGetProperty("targetId", out var tidElem) && tidElem.GetInt64() == userId)
                            {
                                if (item.TryGetProperty("imageUrl", out var imgElem) && imgElem.ValueKind == JsonValueKind.String)
                                    return imgElem.GetString();
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_AVATAR_URL, $"Failed parsing thumbnail response for {userId}: {ex.Message}");
                }

                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_AVATAR_URL, $"Exception: {ex.Message}");
                return null;
            }
        }

        private async Task FetchFriendsAsync(long userId, CancellationToken token = default)
        {
            const string LOG_IDENT_FRIENDS = $"{LOG_IDENT}::FetchFriends";

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() => Friends.Clear());

                if (userId == 0)
                {
                    App.Logger.WriteLine(LOG_IDENT_FRIENDS, "UserId is 0, skipping friends fetch");
                    return;
                }

                var mgr = GetManager();
                if (mgr == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_FRIENDS, "Account manager unavailable.");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_FRIENDS, $"Starting friends fetch for user {userId}");
                var friendsData = await FetchFriendsListAsync(userId, token);

                if (!friendsData.Any())
                {
                    return;
                }

                var onlineFriendIds = friendsData
                    .Where(f => f.IsOnline || f.PresenceType == 2)
                    .Select(f => f.Id)
                    .ToList();

                Dictionary<long, UserPresence> presenceMap = new();
                if (onlineFriendIds.Any())
                {
                    presenceMap = await FetchPresenceForUsersAsync(userId, onlineFriendIds, token);
                }

                var friendIds = friendsData.Select(f => f.Id).ToList();
                var avatarUrls = await FetchAvatarsConcurrentlyAsync(friendIds, token);

                var friendsInGames = friendsData.Where(f =>
                    presenceMap.TryGetValue(f.Id, out var presence) &&
                    presence?.UserPresenceType == 2
                ).ToList();

                var gameNameTasks = friendsInGames.Select(async friend =>
                {
                    presenceMap.TryGetValue(friend.Id, out var presence);
                    return (friend.Id, GameName: await GetGameNameFromPresence(presence));
                }).ToList();

                var gameNameResults = await Task.WhenAll(gameNameTasks);
                var gameNameMap = gameNameResults.ToDictionary(x => x.Id, x => x.GameName);

                var friendList = new List<FriendInfo>();

                foreach (var friend in friendsData)
                {
                    token.ThrowIfCancellationRequested();

                    var avatarUrl = avatarUrls.GetValueOrDefault(friend.Id);

                    int presenceType = friend.PresenceType;
                    string lastLocation = "Offline";
                    string playingGameName = "";

                    if (presenceMap.TryGetValue(friend.Id, out var friendPresence))
                    {
                        presenceType = friendPresence.UserPresenceType;
                        lastLocation = friendPresence.LastLocation ?? "Online";

                        if (presenceType == 2)
                        {
                            playingGameName = gameNameMap.GetValueOrDefault(friend.Id, "");
                            if (string.IsNullOrWhiteSpace(playingGameName))
                                playingGameName = lastLocation;
                        }
                    }
                    else if (friend.IsOnline)
                    {
                        lastLocation = "Online";
                    }

                    string displayLocation = GetDisplayLocation(presenceType, playingGameName, lastLocation);
                    string statusColor = GetStatusColor(presenceType);

                    var friendInfo = new FriendInfo(
                        friend.Id,
                        friend.DisplayName,
                        string.IsNullOrEmpty(avatarUrl) ? null : avatarUrl,
                        presenceType,
                        displayLocation,
                        statusColor,
                        playingGameName
                    );

                    friendList.Add(friendInfo);
                }

                var orderedFriends = friendList
                    .OrderByDescending(f => f.PresenceType)
                    .ThenBy(f => f.PresenceType == 1 ? 0 : 1)
                    .ThenBy(f => f.DisplayName)
                    .ToList();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Friends.Clear();
                    foreach (var f in orderedFriends)
                        Friends.Add(f);

                    FilterFriends();
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::FetchFriends", $"Exception: {ex.Message}");
            }
        }

        private async Task<List<FriendData>> FetchFriendsListAsync(long userId, CancellationToken token)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                string url = $"https://friends.roblox.com/v1/users/{userId}/friends";
                var response = await client.GetAsync(url, token);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<FriendData>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var data = json["data"] as JArray;

                if (data == null || !data.Any())
                    return new List<FriendData>();

                var friends = new List<FriendData>();
                foreach (var friend in data)
                {
                    token.ThrowIfCancellationRequested();

                    long? id = friend["id"]?.Value<long>();
                    if (id.HasValue && id > 0)
                    {
                        friends.Add(new FriendData
                        {
                            Id = id.Value,
                            DisplayName = friend["displayName"]?.ToString() ?? friend["name"]?.ToString() ?? id.Value.ToString(),
                            IsOnline = friend["isOnline"]?.Value<bool>() ?? false,
                            PresenceType = friend["presenceType"]?.Value<int>() ?? 0
                        });
                    }
                }

                return friends;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FetchFriendsListAsync", $"Exception: {ex.Message}");
                return new List<FriendData>();
            }
        }

        private async Task<Dictionary<long, UserPresence>> FetchPresenceForUsersAsync(long userId, List<long> userIds, CancellationToken token)
        {
            if (!userIds.Any())
                return new Dictionary<long, UserPresence>();

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                var requestBody = new { userIds = userIds };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var mgr = GetManager();
                string? cookie = mgr?.GetRoblosecurityForUser(userId);
                if (!string.IsNullOrEmpty(cookie))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                var response = await client.PostAsync("https://presence.roblox.com/v1/presence/users", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var presenceData = JsonSerializer.Deserialize<PresenceResponse>(responseContent)?.UserPresences;
                    return presenceData?.ToDictionary(p => p.UserId, p => p) ?? new Dictionary<long, UserPresence>();
                }

                return new Dictionary<long, UserPresence>();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::FetchFriends", $"Exception: {ex.Message}");
                return new Dictionary<long, UserPresence>();
            }
        }

        private string GetDisplayLocation(int presenceType, string playingGameName, string lastLocation)
        {
            return presenceType switch
            {
                1 => "On Website",
                2 => !string.IsNullOrEmpty(playingGameName) ? playingGameName : "In Game",
                3 => "In Studio",
                _ => "Offline"
            };
        }

        private string GetStatusColor(int presenceType)
        {
            return presenceType switch
            {
                0 => "#808080",
                1 => "#00A2FF",
                2 => "#02B757",
                3 => "#ffa500",
                _ => "#808080"
            };
        }

        private void FilterFriends()
        {
            if (Friends == null || !Friends.Any())
            {
                FilteredFriends.Clear();
                return;
            }

            var filtered = SelectedFriendFilter switch
            {
                "Online" => Friends.Where(f => f.PresenceType == 2).ToList(),
                "Website" => Friends.Where(f => f.PresenceType == 1).ToList(),
                "Studio" => Friends.Where(f => f.PresenceType == 3).ToList(),
                "Offline" => Friends.Where(f => f.PresenceType != 1 && f.PresenceType != 2 && f.PresenceType != 3).ToList(),
                _ => Friends.ToList()
            };

            var ordered = filtered
                .OrderByDescending(f => f.PresenceType == 3)
                .ThenByDescending(f => f.PresenceType == 2)
                .ThenByDescending(f => f.PresenceType == 1)
                .ThenBy(f => f.DisplayName)
                .ToList();

            FilteredFriends.Clear();
            foreach (var friend in ordered)
                FilteredFriends.Add(friend);
        }

        partial void OnSelectedFriendFilterChanged(string value)
        {
            FilterFriends();
        }

        private async Task LoadAvatarImage(long userId)
        {
            const string LOG_IDENT_AVATAR_IMG = $"{LOG_IDENT}::LoadAvatarImage";

            try
            {
                string? imageUrl = await GetAvatarUrl(userId);

                if (imageUrl != null)
                {
                    var bitmap = await LoadBitmapFromUrlAsync(imageUrl);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentUserAvatar = bitmap;
                    });
                }
                else
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentUserAvatar = null;
                    });
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_AVATAR_IMG, $"Exception: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentUserAvatar = null;
                });
            }
        }

        private async Task<(int friends, int followers, int following)> GetAccountInformationAsync(long userId)
        {
            if (userId == 0)
                return (0, 0, 0);

            try
            {
                using var client = new HttpClient();

                var friendsTask = client.GetAsync($"https://friends.roblox.com/v1/users/{userId}/friends/count");
                var followersTask = client.GetAsync($"https://friends.roblox.com/v1/users/{userId}/followers/count");
                var followingTask = client.GetAsync($"https://friends.roblox.com/v1/users/{userId}/followings/count");

                await Task.WhenAll(friendsTask, followersTask, followingTask);

                async Task<int> ParseCount(HttpResponseMessage response)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var json = JObject.Parse(content);
                        return json["count"]?.Value<int>() ?? 0;
                    }
                    return 0;
                }

                var friendsCount = await ParseCount(friendsTask.Result);
                var followersCount = await ParseCount(followersTask.Result);
                var followingCount = await ParseCount(followingTask.Result);

                return (friendsCount, followersCount, followingCount);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::GetAccountInformation", $"Exception: {ex.Message}");
                return (0, 0, 0);
            }
        }

        private async Task UpdateAccountInformationAsync(long userId)
        {
            if (userId == 0)
            {
                IsAccountInformationVisible = false;
                return;
            }

            try
            {
                var (friends, followers, following) = await GetAccountInformationAsync(userId);

                FriendsCount = friends;
                FollowersCount = followers;
                FollowingCount = following;

                IsAccountInformationVisible = true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::UpdateAccountInformation", $"Exception: {ex.Message}");
                IsAccountInformationVisible = false;
            }
        }

        partial void OnSelectedRegionChanged(string? value)
        {
            const string LOG_IDENT_REGION = $"{LOG_IDENT}::OnSelectedRegionChanged";

            var mgr = GetManager();
            if (mgr != null && value != null)
            {
                mgr.SetSelectedRegion(value);
                App.Logger.WriteLine(LOG_IDENT_REGION, $"Selected region changed to: {value}");
            }
        }

        private async Task SwitchToAccountAsync(AltAccount account)
        {
            const string LOG_IDENT_SWITCH = $"{LOG_IDENT}::SwitchToAccount";

            CurrentUserDisplayName = account.DisplayName;
            CurrentUserUsername = $"@{account.Username}";

            _ = Task.Run(async () =>
            {
                try
                {
                    string? avatarUrl = await GetAvatarUrl(account.UserId);
                    CurrentUserAvatarUrl = avatarUrl ?? "";

                    if (avatarUrl != null)
                    {
                        var bitmap = await LoadBitmapFromUrlAsync(avatarUrl);
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentUserAvatar = bitmap;
                        });
                    }
                    else
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentUserAvatar = null;
                        });
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine($"{LOG_IDENT_SWITCH}::AvatarLoad", $"Exception: {ex.Message}");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentUserAvatar = null;
                    });
                }
            });

            await UpdateAccountInformationAsync(account.UserId);

            _ = CheckPresenceAsync();

            _ = RefreshFriends();

            _ = RefreshDiscoveryGames();
            await RefreshFavoriteGames();
            await RefreshContinuePlaying();

            OnPropertyChanged(nameof(ShouldShowGames));
        }

        private async Task DebouncedSearchTriggerAsync(CancellationToken token)
        {
            const string LOG_IDENT_SEARCH_DEBOUNCE = $"{LOG_IDENT}::DebouncedSearchTrigger";

            try
            {
                await Task.Delay(600, token);
                if (token.IsCancellationRequested)
                    return;

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    await SearchGamesAsync();
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_SEARCH_DEBOUNCE, $"Exception: {ex.Message}");
            }
        }

        private async Task SearchGamesAsync()
        {
            const string LOG_IDENT_SEARCH = $"{LOG_IDENT}::SearchGames";

            SearchResults.Clear();

            try
            {
                var results = await GameSearching.GetGameSearchResultsAsync(SearchQuery);
                foreach (var r in results)
                    SearchResults.Add(r);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_SEARCH, $"Exception: {ex.Message}");
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;
            _ = DebouncedSearchTriggerAsync(token);

            if (long.TryParse(value, out long placeId))
            {
                PlaceId = value;
                _ = LoadGameThumbnailAsync(placeId);
                _ = LoadSubplacesForSelectedGameAsync();
            }
        }

        private async Task LoadGameDetailsAsync(long placeId)
        {
            const string LOG_IDENT_GAME_DETAILS = $"{LOG_IDENT}::LoadGameDetails";

            if (placeId == 0)
            {
                ResetGameDetails();
                return;
            }

            try
            {
                var placeDetails = await FetchPlaceDetailsAsync(placeId);
                if (placeDetails != null)
                {
                    SelectedGameName = placeDetails.name ?? "Unknown Game";
                    SelectedGameCreator = placeDetails.builder ?? "Unknown Creator";
                    IsSelectedGameCreatorVerified = placeDetails.hasVerifiedBadge;
                }
                else
                {
                    SelectedGameName = "Unknown Game";
                    SelectedGameCreator = "Unknown Creator";
                    IsSelectedGameCreatorVerified = false;
                }

                await LoadUniverseStatsAsync(placeId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_GAME_DETAILS, $"Exception: {ex.Message}");
                ResetGameDetails();
            }
        }

        private async Task LoadUniverseStatsAsync(long placeId)
        {
            try
            {
                var placeDetails = await FetchPlaceDetailsAsync(placeId);
                if (placeDetails == null)
                {
                    await UniverseDetails.FetchBulk(placeId.ToString());
                    var fallbackUniverseDetails = UniverseDetails.LoadFromCache(placeId);

                    if (fallbackUniverseDetails?.Data != null)
                    {
                        SelectedGameVisits = fallbackUniverseDetails.Data.Visits;
                        SelectedGamePlaying = (int?)fallbackUniverseDetails.Data.Playing;
                    }
                    else
                    {
                        SelectedGameVisits = 0;
                        SelectedGamePlaying = 0;
                    }
                    return;
                }

                long universeId = placeDetails.universeId;

                await UniverseDetails.FetchBulk(universeId.ToString());
                var universeDetails = UniverseDetails.LoadFromCache(universeId);

                if (universeDetails?.Data != null)
                {
                    SelectedGameVisits = universeDetails.Data.Visits;
                    SelectedGamePlaying = (int?)universeDetails.Data.Playing;
                }
                else
                {
                    SelectedGameVisits = 0;
                    SelectedGamePlaying = 0;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::LoadUniverseStats", $"Exception: {ex.Message}");
                SelectedGameVisits = 0;
                SelectedGamePlaying = 0;
            }
        }

        private void ResetGameDetails()
        {
            SelectedGameName = "";
            SelectedGameCreator = "";
            IsSelectedGameCreatorVerified = false;
            SelectedGameVisits = 0;
            SelectedGamePlaying = 0;
            SelectedGameThumbnail = null;
        }

        private async Task<PlaceDetails?> FetchPlaceDetailsAsync(long placeId)
        {
            const string LOG_IDENT_PLACE_DETAILS = $"{LOG_IDENT}::FetchPlaceDetails";

            try
            {
                using var client = new HttpClient();
                string url = $"https://games.roblox.com/v1/games/multiget-place-details?placeIds={placeId}";

                var mgr = GetManager();
                if (mgr?.ActiveAccount != null)
                {
                    string? cookie = mgr.GetRoblosecurityForUser(mgr.ActiveAccount.UserId);
                    if (!string.IsNullOrEmpty(cookie))
                    {
                        client.DefaultRequestHeaders.Add("Cookie", $".ROBLOSECURITY={cookie}");
                    }
                }

                var response = await client.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    App.Logger.WriteLine(LOG_IDENT_PLACE_DETAILS, $"Unauthorized access to place details for {placeId}. Authentication may be required.");
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_PLACE_DETAILS, $"Failed to fetch place details: {response.StatusCode}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var placeDetailsArray = JsonSerializer.Deserialize<List<PlaceDetails>>(responseContent);

                return placeDetailsArray?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_PLACE_DETAILS, $"Exception: {ex.Message}");
                return null;
            }
        }

        private async Task CheckPresenceAsync()
        {
            const string LOG_IDENT_PRESENCE = $"{LOG_IDENT}::CheckPresence";

            var mgr = GetManager();
            if (mgr?.ActiveAccount is null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentUserPresence = null;
                    IsPlayingGame = false;
                    CurrentUserPlayingGame = "";
                });
                return;
            }

            if (IsPresenceLoading)
                return;

            try
            {
                IsPresenceLoading = true;

                var activeUserId = mgr.ActiveAccount.UserId;

                List<long> friendIds = await GetFriendIdsAsync(activeUserId);

                var ids = new List<long> { activeUserId };
                ids.AddRange(friendIds.Where(id => id != activeUserId));
                ids = ids.Distinct().ToList();

                Dictionary<long, UserPresence>? presenceData = null;
                if (ids.Any())
                {
                    presenceData = await FetchPresenceForUsersAsync(activeUserId, ids, CancellationToken.None);
                }

                if (presenceData == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_PRESENCE, "Presence API returned null");

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (CurrentUserPresence == null)
                            CurrentUserPresence = new AccountPresence(0, "Offline", "#808080", "Offline");

                        IsPlayingGame = false;
                        CurrentUserPlayingGame = "";
                    });

                    return;
                }

                var presenceMap = presenceData;

                if (presenceMap.TryGetValue(activeUserId, out var activePresence))
                {
                    string statusColor;
                    string tooltipText;
                    string lastLocation;

                    switch (activePresence.UserPresenceType)
                    {
                        case 1:
                            statusColor = "#00a2ff";
                            tooltipText = "On Website";
                            lastLocation = "On Website";
                            break;
                        case 2:
                            statusColor = "#02b757";
                            tooltipText = "In Game";

                            string gameName = await GetGameNameFromPresence(activePresence);
                            if (!string.IsNullOrWhiteSpace(gameName))
                            {
                                lastLocation = gameName;
                                tooltipText = $"Playing {gameName}";
                            }
                            else
                            {
                                lastLocation = activePresence.LastLocation ?? "Playing";
                                tooltipText = "In Game";
                            }
                            break;
                        case 3:
                            statusColor = "#ffa500";
                            tooltipText = "In Studio";
                            lastLocation = "Roblox Studio";
                            break;
                        default:
                            statusColor = "#808080";
                            tooltipText = "Offline";
                            lastLocation = "Offline";
                            break;
                    }

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentUserPresence = new AccountPresence(activePresence.UserPresenceType, lastLocation, statusColor, tooltipText);
                    });

                    if (activePresence.UserPresenceType == 2)
                    {
                        string gameName = await GetGameNameFromPresence(activePresence);

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            IsPlayingGame = true;
                            CurrentUserPlayingGame = string.IsNullOrWhiteSpace(gameName) ? lastLocation : gameName;
                        });
                    }
                    else
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            IsPlayingGame = false;
                            CurrentUserPlayingGame = "";
                        });
                    }
                }
                else
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentUserPresence = new AccountPresence(0, "Offline", "#808080", "Offline");
                        IsPlayingGame = false;
                        CurrentUserPlayingGame = "";
                    });
                }

                if (Friends != null && Friends.Any())
                {
                    var updatedFriends = new List<FriendInfo>();

                    var onlineFriends = Friends.Where(f =>
                        presenceMap.TryGetValue(f.Id, out var pres) &&
                        pres?.UserPresenceType == 2
                    ).ToList();

                    var gameNameTasks = onlineFriends.Select(async friend =>
                    {
                        presenceMap.TryGetValue(friend.Id, out var fPres);
                        return (friend.Id, GameName: await GetGameNameFromPresence(fPres));
                    }).ToList();

                    var gameNameResults = await Task.WhenAll(gameNameTasks);
                    var gameNameMap = gameNameResults.ToDictionary(x => x.Id, x => x.GameName);

                    foreach (var friend in Friends.ToList())
                    {
                        try
                        {
                            presenceMap.TryGetValue(friend.Id, out var fPres);
                            int presenceType = fPres?.UserPresenceType ?? 0;
                            string lastLocation = "Offline";
                            string playingGameName = "";

                            if (fPres != null && presenceType > 0)
                            {
                                lastLocation = fPres.LastLocation ?? "Online";

                                if (presenceType == 2)
                                {
                                    playingGameName = gameNameMap.GetValueOrDefault(friend.Id, "");
                                    if (string.IsNullOrWhiteSpace(playingGameName))
                                        playingGameName = lastLocation;
                                }
                            }

                            string statusColor = presenceType switch
                            {
                                1 => "#0078D4",
                                2 => "#107C10",
                                3 => "#FFA500",
                                _ => "#808080"
                            };

                            string displayLocation = presenceType switch
                            {
                                1 => "On Website",
                                2 => string.IsNullOrWhiteSpace(playingGameName) ? "In Game" : playingGameName,
                                3 => "In Studio",
                                _ => "Offline"
                            };

                            var newFriend = new FriendInfo(
                                friend.Id,
                                friend.DisplayName,
                                friend.AvatarUrl,
                                presenceType,
                                displayLocation,
                                statusColor,
                                playingGameName
                            );
                            updatedFriends.Add(newFriend);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine($"{LOG_IDENT_PRESENCE}::UpdateFriend({friend.Id})", $"Exception: {ex.Message}");
                            updatedFriends.Add(friend);
                        }
                    }

                    var orderedUpdatedFriends = updatedFriends
                        .OrderByDescending(f => f.PresenceType)
                        .ThenBy(f => f.DisplayName)
                        .ToList();

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Friends.Clear();
                        foreach (var nf in orderedUpdatedFriends)
                            Friends.Add(nf);

                        FilterFriends();
                    });
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_PRESENCE, $"Exception: {ex.Message}");
                PresenceStatus = "Failed to update presence";

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (CurrentUserPresence == null)
                        CurrentUserPresence = new AccountPresence(0, "Offline", "#808080", "Offline");

                    IsPlayingGame = false;
                    CurrentUserPlayingGame = "";
                });
            }
            finally
            {
                IsPresenceLoading = false;
            }
        }

        private async Task<List<long>> GetFriendIdsAsync(long userId)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                string url = $"https://friends.roblox.com/v1/users/{userId}/friends?userSort=2";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<long>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var data = json["data"] as JArray;

                if (data == null || !data.Any())
                    return new List<long>();

                return data
                    .Select(friend => friend["id"]?.Value<long>() ?? 0)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GetFriendIdsAsync", $"Exception: {ex.Message}");
                return new List<long>();
            }
        }

        private async Task<string> GetGameNameFromPresence(UserPresence? presence)
        {
            const string LOG_IDENT_GAME_NAME = $"{LOG_IDENT}::GetGameNameFromPresence";

            if (presence == null)
                return "";

            try
            {
                string gameName = "";

                if (string.IsNullOrWhiteSpace(gameName) && presence.RootPlaceId.HasValue && presence.RootPlaceId.Value != 0)
                {
                    try
                    {
                        var pd = await FetchPlaceDetailsAsync(presence.RootPlaceId.Value);
                        if (pd?.name != null)
                        {
                            gameName = pd.name;
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine($"{LOG_IDENT_GAME_NAME}::RootPlaceId", $"Exception: {ex.Message}");
                    }
                }

                if (string.IsNullOrWhiteSpace(gameName) && !string.IsNullOrWhiteSpace(presence.GameId) && long.TryParse(presence.GameId, out long gameId) && gameId != 0)
                {
                    try
                    {
                        var pd = await FetchPlaceDetailsAsync(gameId);
                        if (pd?.name != null)
                        {
                            gameName = pd.name;
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine($"{LOG_IDENT_GAME_NAME}::GameId", $"Exception: {ex.Message}");
                    }
                }

                if (string.IsNullOrWhiteSpace(gameName) && presence.PlaceId.HasValue && presence.PlaceId.Value != 0)
                {
                    try
                    {
                        var pd = await FetchPlaceDetailsAsync(presence.PlaceId.Value);
                        if (pd?.name != null)
                        {
                            gameName = pd.name;
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine($"{LOG_IDENT_GAME_NAME}::PlaceId", $"Exception: {ex.Message}");
                    }
                }

                if (string.IsNullOrWhiteSpace(gameName) && presence.UniverseId.HasValue && presence.UniverseId.Value != 0)
                {
                    try
                    {
                        await UniverseDetails.FetchBulk(presence.UniverseId.Value.ToString());
                        var ud = UniverseDetails.LoadFromCache(presence.UniverseId.Value);
                        if (ud?.Data != null && !string.IsNullOrWhiteSpace(ud.Data.Name))
                        {
                            gameName = ud.Data.Name;
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine($"{LOG_IDENT_GAME_NAME}::UniverseId", $"Exception: {ex.Message}");
                    }
                }

                return gameName;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_GAME_NAME, $"Exception: {ex.Message}");
                return "";
            }
        }

        private async Task LoadSubplacesForSelectedGameAsync()
        {
            const string LOG_IDENT_SUBPLACES = $"{LOG_IDENT}::LoadSubplacesForSelectedGame";

            try
            {
                var placeDetails = await FetchPlaceDetailsAsync(long.Parse(PlaceId));
                if (placeDetails == null || placeDetails.universeId == 0)
                {
                    Subplaces.Clear();
                    return;
                }

                await FetchSubplacesAsync(placeDetails.universeId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_SUBPLACES, $"Exception: {ex.Message}");
                Subplaces.Clear();
            }
        }

        private async Task FetchSubplacesAsync(long universeId)
        {
            const string LOG_IDENT_FETCH_SUBPLACES = $"{LOG_IDENT}::FetchSubplaces";

            if (universeId == 0)
            {
                Subplaces.Clear();
                return;
            }

            try
            {
                IsLoadingSubplaces = true;

                using var client = new HttpClient();
                string url = $"https://develop.roblox.com/v1/universes/{universeId}/places?isUniverseCreation=false&limit=100&sortOrder=Asc";

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_FETCH_SUBPLACES, $"Failed to fetch subplaces: {response.StatusCode}");
                    return;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var subplacesResponse = JsonSerializer.Deserialize<SubplacesResponse>(responseContent);

                if (subplacesResponse?.Data == null || !subplacesResponse.Data.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_FETCH_SUBPLACES, "No subplaces found in response");
                    Subplaces.Clear();
                    return;
                }

                var subplacesList = new List<PlaceInfo>();

                foreach (var place in subplacesResponse.Data)
                {
                    string thumbnailUrl = await GetPlaceThumbnailUrlAsync(place.Id);
                    subplacesList.Add(new PlaceInfo(place.Id, place.UniverseId, place.Name, thumbnailUrl));
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Subplaces.Clear();
                    foreach (var subplace in subplacesList)
                        Subplaces.Add(subplace);
                });

                App.Logger.WriteLine(LOG_IDENT_FETCH_SUBPLACES, $"Loaded {subplacesList.Count} subplaces for universe {universeId}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_FETCH_SUBPLACES, $"Exception: {ex.Message}");
                Subplaces.Clear();
            }
            finally
            {
                IsLoadingSubplaces = false;
            }
        }

        private async Task<string> GetPlaceThumbnailUrlAsync(long placeId)
        {
            try
            {
                var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                    $"https://thumbnails.roblox.com/v1/places/gameicons?placeIds={placeId}&returnPolicy=PlaceHolder&size=50x50&format=Png&isCircular=false");

                return thumbnailResponse?.Data?.FirstOrDefault()?.ImageUrl ?? "";
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::GetPlaceThumbnailUrl", $"Exception: {ex.Message}");
                return "";
            }
        }

        private async Task LoadGameThumbnailAsync(long placeId)
        {
            const string LOG_IDENT_THUMBNAIL = $"{LOG_IDENT}::LoadGameThumbnail";

            if (placeId == 0)
            {
                ResetGameDetails();
                return;
            }

            try
            {
                var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                    $"https://thumbnails.roblox.com/v1/places/gameicons?placeIds={placeId}&returnPolicy=PlaceHolder&size=256x256&format=Png&isCircular=false");

                if (thumbnailResponse?.Data != null && thumbnailResponse.Data.Any())
                {
                    var thumbnail = thumbnailResponse.Data.First();
                    SelectedGameThumbnail = thumbnail.ImageUrl;
                }
                else
                {
                    SelectedGameThumbnail = null;
                }

                await LoadGameDetailsAsync(placeId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_THUMBNAIL, $"Exception: {ex.Message}");
                ResetGameDetails();
            }
        }

        partial void OnSelectedSearchResultChanged(GameSearchResult? value)
        {
            const string LOG_IDENT_SEARCH_RESULT = $"{LOG_IDENT}::OnSelectedSearchResultChanged";

            if (value != null)
            {
                PlaceId = value.RootPlaceId.ToString();
                SearchQuery = value.RootPlaceId.ToString();

                _ = LoadGameThumbnailAsync(value.RootPlaceId);

                _ = LoadSubplacesForSelectedGameAsync();

                _searchDebounceCts?.Cancel();
                App.Logger.WriteLine(LOG_IDENT_SEARCH_RESULT, $"Selected game: {value.Name} ({PlaceId})");
            }
            else
            {
                ResetGameDetails();
                Subplaces.Clear();
            }
        }

        private async Task FindAndJoinServer(long placeId, AltAccount account)
        {
            const string LOG_IDENT_FIND_SERVER = $"{LOG_IDENT}::FindAndJoinServer";

            var fetcher = new RobloxServerFetcher();
            string? nextCursor = "";
            int attemptCount = 0;
            const int maxAttempts = 20;

            AutoSearchStatus = "Loading regions...";
            var datacentersResult = await fetcher.GetDatacentersAsync();

            if (datacentersResult == null)
            {
                Frontend.ShowMessageBox("Failed to load server regions. Please try again later.", MessageBoxImage.Error);
                return;
            }

            var (regions, dcMap) = datacentersResult.Value;

            if (!Regions.Any())
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Regions.Clear();
                    foreach (var region in regions)
                        Regions.Add(region);
                });
            }

            fetcher.SetRoblosecurity(account.SecurityToken);

            var mgr = GetManager();
            if (mgr == null)
            {
                Frontend.ShowMessageBox("Account manager is not available. Please restart the application.", MessageBoxImage.Error);
                return;
            }

            while (attemptCount < maxAttempts)
            {
                attemptCount++;
                AutoSearchStatus = $"Searching servers... (Page {attemptCount})";

                var result = await fetcher.FetchServerInstancesAsync(placeId, nextCursor, SelectedSortOrder);

                if (result?.Servers == null || !result.Servers.Any())
                {
                    AutoSearchStatus = "No servers found, checking next page...";
                    await Task.Delay(1000);
                    continue;
                }

                var matchingServer = result.Servers.FirstOrDefault(server =>
                server.DataCenterId.HasValue &&
                dcMap.TryGetValue(server.DataCenterId.Value, out var mappedRegion) &&
                mappedRegion == SelectedRegion);

                if (matchingServer != null)
                {
                    AutoSearchStatus = $"Found server in {SelectedRegion}! Joining...";

                    await mgr.LaunchAccountToPlaceAsync(account, placeId, matchingServer.Id);

                    AutoSearchStatus = "Successfully joined server!";
                    return;
                }

                AutoSearchStatus = $"Checked {result.Servers.Count} servers on page {attemptCount}, none in {SelectedRegion}. Continuing search...";

                if (!string.IsNullOrEmpty(result.NextCursor))
                {
                    nextCursor = result.NextCursor;
                }
                else
                {
                    Frontend.ShowMessageBox($"Could not find a server in {SelectedRegion} after searching all available servers.", MessageBoxImage.Information);
                    return;
                }

                await Task.Delay(500);
            }

            Frontend.ShowMessageBox($"Could not find a server in {SelectedRegion} after {maxAttempts} pages. Try selecting a different region.", MessageBoxImage.Information);
        }

        private async Task AutoFindAndJoinGameAsync(long placeId)
        {
            const string LOG_IDENT_AUTO_JOIN = $"{LOG_IDENT}::AutoFindAndJoinGameAsync";

            if (placeId == 0)
            {
                Frontend.ShowMessageBox("Invalid Place ID.", MessageBoxImage.Warning);
                return;
            }

            PlaceId = placeId.ToString();

            var mgr = GetManager();
            if (mgr?.ActiveAccount is null)
            {
                Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedRegion))
            {
                Frontend.ShowMessageBox("Please select a region first.", MessageBoxImage.Warning);
                return;
            }

            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowLoading($"Searching for {SelectedRegion} server...");

            IsAutoSearching = true;
            AutoSearchStatus = $"Searching for {SelectedRegion} server...";

            try
            {
                await FindAndJoinServer(placeId, mgr.ActiveAccount);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_AUTO_JOIN, $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to find and join server: {ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                IsAutoSearching = false;
                AutoSearchStatus = "";
                mainWindow?.HideLoading();
            }
        }

        private async Task LaunchGameAsync(long placeId)
        {
            const string LOG_IDENT_LAUNCH = $"{LOG_IDENT}::LaunchGameAsync";

            var mgr = GetManager();
            if (mgr?.ActiveAccount is null)
            {
                Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            if (placeId == 0)
            {
                Frontend.ShowMessageBox("Invalid Place ID.", MessageBoxImage.Warning);
                return;
            }

            PlaceId = placeId.ToString();
            mgr.SetCurrentPlaceId(PlaceId);
            mgr.SetCurrentServerInstanceId(ServerId);

            await mgr.LaunchAccountToPlaceAsync(mgr.ActiveAccount, placeId, ServerId);
        }

        private async Task ExecuteWithCancellationSupport(
            Func<CancellationToken, Task> action,
            CancellationTokenSource? cts,
            string operationName)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            try
            {
                await action(token);
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::{operationName}", "Cancelled.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::{operationName}", $"Exception: {ex.Message}");
            }
        }

        private async Task LoadRegionsAsync()
        {
            const string LOG_IDENT_REGIONS = $"{LOG_IDENT}::LoadRegions";

            Regions.Clear();

            var fetcher = new RobloxServerFetcher();
            try
            {
                var mgr = GetManager();
                var token = mgr?.ActiveAccount?.SecurityToken;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    fetcher.SetRoblosecurity(token);
                    App.Logger.WriteLine(LOG_IDENT_REGIONS, "Using roblosecurity from AccountManager for region requests.");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_REGIONS, "No active account roblosecurity available; fetcher will use cache.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_REGIONS, $"Exception: {ex.Message}");
            }

            var datacentersResult = await fetcher.GetDatacentersAsync();

            if (datacentersResult != null)
            {
                App.Logger.WriteLine(LOG_IDENT_REGIONS, "Successfully loaded datacenters from API, saving to cache...");
                await SaveDatacentersToCacheAsync(datacentersResult.Value);
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT_REGIONS, "Failed to load datacenters from API, trying cache...");

                datacentersResult = await LoadDatacentersFromCacheAsync();

                if (datacentersResult == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_REGIONS, "Failed to load datacenters from cache.");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_REGIONS, "Successfully loaded datacenters from cache");
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var region in datacentersResult.Value.regions)
                    Regions.Add(region);

                var mgr = GetManager();
                if (!string.IsNullOrEmpty(mgr?.SelectedRegion) && Regions.Contains(mgr.SelectedRegion))
                {
                    SelectedRegion = mgr.SelectedRegion;
                }
                else if (string.IsNullOrEmpty(SelectedRegion) && datacentersResult.Value.regions.Count > 0)
                {
                    SelectedRegion = datacentersResult.Value.regions[0];
                }
            });

            App.Logger.WriteLine(LOG_IDENT_REGIONS, $"Loaded {datacentersResult.Value.regions.Count} regions. Selected: {SelectedRegion}");
        }

        private string GetDatacentersCachePath()
        {
            string cacheDir = Paths.Cache;
            Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, "datacenters_cache.json");
        }

        private async Task SaveDatacentersToCacheAsync((List<string> regions, Dictionary<int, string> datacenterMap) datacenters)
        {
            const string LOG_IDENT_CACHE_SAVE = $"{LOG_IDENT}::SaveDatacentersToCache";

            try
            {
                var cacheData = new DatacentersCache
                {
                    Regions = datacenters.regions,
                    DatacenterMap = datacenters.datacenterMap,
                    LastUpdated = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(GetDatacentersCachePath(), json);

                App.Logger.WriteLine(LOG_IDENT_CACHE_SAVE, "Successfully saved datacenters to cache");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_CACHE_SAVE, $"Exception: {ex.Message}");
            }
        }

        private async Task<(List<string> regions, Dictionary<int, string> datacenterMap)?> LoadDatacentersFromCacheAsync()
        {
            const string LOG_IDENT_CACHE_LOAD = $"{LOG_IDENT}::LoadDatacentersFromCache";

            try
            {
                var cachePath = GetDatacentersCachePath();

                if (!File.Exists(cachePath))
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, "Cache file does not exist");
                    return null;
                }

                var json = await File.ReadAllTextAsync(cachePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, "Cache file is empty");
                    return null;
                }

                var cacheData = JsonSerializer.Deserialize<DatacentersCache>(json);
                if (cacheData == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, "Failed to deserialize cache JSON");
                    return null;
                }

                if (cacheData.LastUpdated < DateTime.UtcNow.AddDays(-7))
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, "Cache is too old, ignoring");
                    return null;
                }

                App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, $"Loaded datacenters from cache (last updated: {cacheData.LastUpdated})");
                return (cacheData.Regions, cacheData.DatacenterMap);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_CACHE_LOAD, $"Exception: {ex.Message}");
                return null;
            }
        }

        private void UpdateBackendAccountOrder()
        {
            const string LOG_IDENT_ORDER = $"{LOG_IDENT}::UpdateBackendAccountOrder";

            var mgr = GetManager();
            if (mgr is null) return;

            var newOrder = new List<AltAccount>();

            foreach (var uiAccount in Accounts)
            {
                var backendAccount = mgr.Accounts.FirstOrDefault(backend => backend.UserId == uiAccount.Id);
                if (backendAccount != null)
                    newOrder.Add(backendAccount);
            }

            var accountsField = typeof(AccountManager).GetField("_accounts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (accountsField != null && newOrder.Count == mgr.Accounts.Count)
            {
                var internalAccounts = accountsField.GetValue(mgr) as List<AltAccount>;
                if (internalAccounts != null)
                {
                    internalAccounts.Clear();
                    internalAccounts.AddRange(newOrder);
                    mgr.SaveAccounts();
                }
            }
        }

        partial void OnPlaceIdChanged(string value)
        {
            const string LOG_IDENT_PLACEID = $"{LOG_IDENT}::OnPlaceIdChanged";

            var mgr = GetManager();
            if (mgr != null)
            {
                mgr.SetCurrentPlaceId(value);
            }
        }

        partial void OnServerIdChanged(string value)
        {
            const string LOG_IDENT_SERVERID = $"{LOG_IDENT}::OnServerIdChanged";

            var mgr = GetManager();
            if (mgr != null)
            {
                mgr.SetCurrentServerInstanceId(value);
            }
        }

        private async Task FetchDiscoveryPageGamesAsync(long userId, CancellationToken token = default)
        {
            const string LOG_IDENT_DISCOVERY = $"{LOG_IDENT}::FetchDiscoveryPageGames";

            try
            {
                DiscoveryGames.Clear();

                if (!ShouldShowGames)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "No accounts available, skipping ContinuePlaying games fetch.");
                    return;
                }

                var mgr = GetManager();
                if (mgr == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "Account manager unavailable.");
                    return;
                }

                if (userId == 0 && mgr.ActiveAccount is null)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, " no active account.");
                    return;
                }

                string? cookie = mgr.GetRoblosecurityForUser(userId);
                if (string.IsNullOrEmpty(cookie))
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, " .ROBLOSECURITY not available for user; aborting recent-games fetch.");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $" starting for user {userId}.");

                var postReq = new HttpRequestMessage(HttpMethod.Post, "https://apis.roblox.com/discovery-api/omni-recommendation");
                postReq.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
                postReq.Content = new StringContent("{\"pageType\":\"Home\",\"sessionId\":\"1\"}", Encoding.UTF8, "application/json");

                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, " sending discovery POST...");
                using var resp = await _http.SendAsync(postReq, token).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $" discovery response status={(int)resp.StatusCode}");

                if (!resp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $" discovery POST failed: {(int)resp.StatusCode}. Body: {body}");
                    return;
                }

                JObject jo;
                try
                {
                    jo = JObject.Parse(body);
                }
                catch (Exception parseEx)
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Parse exception: {parseEx.Message}");
                    return;
                }

                var orderedContentIds = new List<long>();
                var sorts = jo["sorts"] as JArray;
                if (sorts != null)
                {
                    foreach (var sort in sorts)
                    {
                        var recs = sort["recommendationList"] as JArray;
                        if (recs == null) continue;
                        foreach (var rec in recs)
                        {
                            if (rec["contentType"]?.Value<string>() != "Game") continue;
                            long? cid = rec["contentId"]?.Value<long?>();
                            if (cid.HasValue) orderedContentIds.Add(cid.Value);
                        }
                    }
                }

                if (!orderedContentIds.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "No game recommendations found in discovery response.");
                    return;
                }

                const int MaxTotalContentIds = 50;
                orderedContentIds = orderedContentIds.Take(MaxTotalContentIds).ToList();
                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Found {orderedContentIds.Count} recommended contentIds (taking first {MaxTotalContentIds}).");

                var universeOrdered = new List<long>();
                var seenUniverses = new HashSet<long>();

                foreach (var cid in orderedContentIds)
                {
                    if (token.IsCancellationRequested) break;

                    var node = jo["contentMetadata"]?["Game"]?[cid.ToString()];
                    if (node == null)
                    {
                        App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $" metadata missing for contentId {cid} - skipping.");
                        continue;
                    }

                    long? universeId = node["universeId"]?.Value<long?>();
                    long? rootPlaceId = node["rootPlaceId"]?.Value<long?>();

                    if (!universeId.HasValue)
                    {
                        App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $" universeId missing for contentId {cid} - skipping.");
                        continue;
                    }

                    long uId = universeId.Value;

                    if (seenUniverses.Contains(uId))
                    {
                        App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $" universe {uId} already queued, skipping duplicate (content {cid}).");
                        continue;
                    }

                    seenUniverses.Add(uId);
                    universeOrdered.Add(uId);
                }

                if (!universeOrdered.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, " no universe ids extracted from metadata.");
                    return;
                }

                var uniqUniverseIds = universeOrdered.Take(50).ToList();

                await UniverseDetails.FetchBulk(string.Join(',', uniqUniverseIds));

                var fetchedGamesOrdered = new List<RecentGameInfo>();

                foreach (var uId in uniqUniverseIds)
                {
                    if (token.IsCancellationRequested) break;

                    var universeDetails = UniverseDetails.LoadFromCache(uId);
                    if (universeDetails?.Data != null)
                    {
                        var gameInfo = new RecentGameInfo(
                            universeDetails.Data.Id,
                            universeDetails.Data.RootPlaceId,
                            universeDetails.Data.Name,
                            (int?)universeDetails.Data.Playing,
                            universeDetails.Data.Visits,
                            universeDetails.Thumbnail?.ImageUrl ?? ""
                        );
                        fetchedGamesOrdered.Add(gameInfo);
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $" game details missing for universe {uId}");
                    }
                }

                await Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    DiscoveryGames.Clear();
                    foreach (var g in fetchedGamesOrdered)
                        DiscoveryGames.Add(g);
                    App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $" finished — added {fetchedGamesOrdered.Count} games to UI (from universeIds).");
                });
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, "Cancelled.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_DISCOVERY, $"Exception: {ex.Message}");
            }
        }

        private async Task FetchContinuePlayingGamesAsync(CancellationToken token = default)
        {
            const string LOG_IDENT_CONTINUE = $"{LOG_IDENT}::FetchContinuePlayingGames";

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ContinuePlayingGames.Clear();
                    IsLoadingContinuePlaying = true;
                });

                if (!ShouldShowGames)
                {
                    App.Logger.WriteLine(LOG_IDENT_CONTINUE, "No active account available, skipping continue playing fetch.");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsLoadingContinuePlaying = false;
                    });
                    return;
                }

                await Task.Delay(100, token);

                var recentGames = await LoadRecentGamesFromCacheAsync();

                if (!recentGames.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_CONTINUE, "No recent games found in cache");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsLoadingContinuePlaying = false;
                    });
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_CONTINUE, $"Found {recentGames.Count} recent games");

                var uniqueRecentGames = recentGames
                    .OrderByDescending(g => g.TimeLeft)
                    .GroupBy(g => g.UniverseId)
                    .Select(g => g.First())
                    .Take(50)
                    .ToList();

                App.Logger.WriteLine(LOG_IDENT_CONTINUE, $"After removing duplicates: {uniqueRecentGames.Count} unique games");

                var continuePlayingList = new List<RecentGameInfo>();
                var universeIdsToFetch = new List<long>();
                var processedUniverseIds = new HashSet<long>();

                var universeIds = uniqueRecentGames.Select(g => g.UniverseId).Distinct().ToList();

                if (universeIds.Any())
                {
                    await UniverseDetails.FetchBulk(string.Join(',', universeIds.Take(50)));
                }

                foreach (var activity in uniqueRecentGames)
                {
                    if (token.IsCancellationRequested) break;

                    if (processedUniverseIds.Contains(activity.UniverseId))
                        continue;

                    processedUniverseIds.Add(activity.UniverseId);

                    var universeDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                    if (universeDetails?.Data != null)
                    {
                        var gameInfo = new RecentGameInfo(
                            activity.UniverseId,
                            universeDetails.Data.RootPlaceId,
                            universeDetails.Data.Name ?? "Unknown Game",
                            (int?)universeDetails.Data.Playing,
                            universeDetails.Data.Visits,
                            universeDetails.Thumbnail?.ImageUrl ?? ""
                        );
                        continuePlayingList.Add(gameInfo);
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT_CONTINUE, $"No universe details found for universe {activity.UniverseId}");
                    }
                }

                continuePlayingList = continuePlayingList
                    .OrderByDescending(g => uniqueRecentGames.First(r => r.UniverseId == g.UniverseId).TimeLeft)
                    .Take(50)
                    .ToList();

                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ContinuePlayingGames.Clear();
                    foreach (var game in continuePlayingList)
                        ContinuePlayingGames.Add(game);

                    IsLoadingContinuePlaying = false;
                }));

                App.Logger.WriteLine(LOG_IDENT_CONTINUE, $"Loaded {continuePlayingList.Count} unique continue playing games");
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT_CONTINUE, "Cancelled.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CONTINUE, ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsLoadingContinuePlaying = false;
                });
            }
        }

        private async Task<List<ActivityData>> LoadRecentGamesFromCacheAsync()
        {
            const string LOG_IDENT_CACHE = $"{LOG_IDENT}::LoadRecentGamesFromCache";

            try
            {
                var cachePath = Path.Combine(Paths.Cache, "GameHistory.json");

                if (!File.Exists(cachePath))
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE, "Game history cache file does not exist");
                    return new List<ActivityData>();
                }

                var json = await File.ReadAllTextAsync(cachePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                };

                var gameHistory = JsonSerializer.Deserialize<List<GameHistoryData>>(json, options);

                if (gameHistory == null || !gameHistory.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_CACHE, "No game history found in cache");
                    return new List<ActivityData>();
                }

                var mgr = GetManager();

                App.Logger.WriteLine(LOG_IDENT_CACHE, $"Raw game history count: {gameHistory.Count}");

                var recentGames = gameHistory
                    .Where(activity =>
                        activity.ServerType == 0 &&
                        activity.UniverseId != 0 &&
                        activity.PlaceId != 0 &&
                        activity.TimeLeft.HasValue)
                    .OrderByDescending(activity => activity.TimeLeft)
                    .Take(15)
                    .Select(history => new ActivityData
                    {
                        UniverseId = history.UniverseId,
                        PlaceId = history.PlaceId,
                        JobId = history.JobId,
                        UserId = history.UserId,
                        ServerType = (ServerType)history.ServerType,
                        TimeJoined = history.TimeJoined,
                        TimeLeft = history.TimeLeft
                    })
                    .ToList();

                App.Logger.WriteLine(LOG_IDENT_CACHE, $"Loaded {recentGames.Count} recent public server games from cache");
                return recentGames;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT_CACHE, ex);
                return new List<ActivityData>();
            }
        }

        private async Task FetchFavoriteGamesAsync(long userId, CancellationToken token = default)
        {
            const string LOG_IDENT_FAVORITES = $"{LOG_IDENT}::FetchFavoriteGames";

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FavoriteGames.Clear();
                });

                if (!ShouldShowGames)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "No accounts available, skipping favorites fetch.");
                    return;
                }

                var mgr = GetManager();
                if (mgr == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "Account manager unavailable.");
                    return;
                }

                if (userId == 0 && mgr.ActiveAccount is null)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "No active account.");
                    return;
                }

                string? cookie = mgr.GetRoblosecurityForUser(userId);
                if (string.IsNullOrEmpty(cookie))
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, ".ROBLOSECURITY not available for user; aborting favorites fetch.");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Starting favorites fetch for user {userId}.");

                var favoritesUrl = $"https://games.roblox.com/v2/users/{userId}/favorite/games?limit=50&sortOrder=Desc";

                var favoritesReq = new HttpRequestMessage(HttpMethod.Get, favoritesUrl);
                favoritesReq.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");

                using var favoritesResp = await _http.SendAsync(favoritesReq, token).ConfigureAwait(false);
                var favoritesBody = await favoritesResp.Content.ReadAsStringAsync().ConfigureAwait(false);

                App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Favorites response status={(int)favoritesResp.StatusCode}");

                if (!favoritesResp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Favorites API failed: {(int)favoritesResp.StatusCode}. Body: {favoritesBody}");
                    return;
                }

                JObject favoritesJson;
                try
                {
                    favoritesJson = JObject.Parse(favoritesBody);
                }
                catch (Exception parseEx)
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Parse exception: {parseEx.Message}");
                    return;
                }

                var favoritesData = favoritesJson["data"] as JArray;
                if (favoritesData == null || !favoritesData.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "No favorite games found.");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Found {favoritesData.Count} favorite games.");

                var universeIds = new List<long>();

                foreach (var game in favoritesData)
                {
                    if (token.IsCancellationRequested) break;

                    long universeId = game["id"]?.Value<long>() ?? 0;
                    if (universeId > 0 && !universeIds.Contains(universeId))
                        universeIds.Add(universeId);
                }

                if (!universeIds.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, "No valid universe IDs found in favorites.");
                    return;
                }

                await UniverseDetails.FetchBulk(string.Join(',', universeIds.Take(50)));

                var favoriteGamesList = new List<RecentGameInfo>();

                foreach (var game in favoritesData)
                {
                    if (token.IsCancellationRequested) break;

                    long universeId = game["id"]?.Value<long>() ?? 0;
                    var rootPlace = game["rootPlace"];
                    long rootPlaceId = rootPlace?["id"]?.Value<long>() ?? 0;

                    var universeDetails = UniverseDetails.LoadFromCache(universeId);
                    if (universeDetails?.Data != null)
                    {
                        var gameInfo = new RecentGameInfo(
                            universeDetails.Data.Id,
                            universeDetails.Data.RootPlaceId,
                            universeDetails.Data.Name,
                            (int?)universeDetails.Data.Playing,
                            universeDetails.Data.Visits,
                            universeDetails.Thumbnail?.ImageUrl ?? ""
                        );
                        favoriteGamesList.Add(gameInfo);
                    }
                    else
                    {
                        var gameInfo = new RecentGameInfo(
                            universeId,
                            rootPlaceId,
                            game["name"]?.Value<string>() ?? "",
                            null,
                            game["placeVisits"]?.Value<long?>(),
                            ""
                        );
                        favoriteGamesList.Add(gameInfo);
                    }
                }

                await Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    FavoriteGames.Clear();
                    foreach (var game in favoriteGamesList)
                        FavoriteGames.Add(game);

                    App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Finished — added {favoriteGamesList.Count} favorite games to UI with full details.");
                });
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT_FAVORITES, "Cancelled.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_FAVORITES, $"Exception: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ShowChangeAccountDialog() => IsChangeAccountDialogOpen = true;

        [RelayCommand]
        private void HideChangeAccountDialog() => IsChangeAccountDialogOpen = false;

        [RelayCommand]
        private void SignOut()
        {
            const string LOG_IDENT_SIGNOUT = $"{LOG_IDENT}::SignOut";

            var mgr = GetManager();
            if (mgr == null) return;

            mgr.SetActiveAccount(null);
            CurrentUserDisplayName = "Not Logged In";
            CurrentUserUsername = "";
            CurrentUserAvatarUrl = "";

            FriendsCount = 0;
            FollowersCount = 0;
            FollowingCount = 0;
            IsAccountInformationVisible = false;

            DiscoveryGames.Clear();
            FavoriteGames.Clear();
            ContinuePlayingGames.Clear();
            SearchResults.Clear();

            Friends.Clear();

            OnPropertyChanged(nameof(ShouldShowGames));
        }

        [RelayCommand]
        private async Task SelectAccount()
        {
            const string LOG_IDENT_SELECT = $"{LOG_IDENT}::SelectAccount";

            if (SelectedAccount is null)
            {
                HideChangeAccountDialog();
                return;
            }

            var mgr = GetManager();
            if (mgr == null)
            {
                HideChangeAccountDialog();
                return;
            }

            bool isSameAccount = mgr.ActiveAccount?.UserId == SelectedAccount.Id;

            var backendAccount = mgr.Accounts.FirstOrDefault(acc => acc.UserId == SelectedAccount.Id);
            if (backendAccount is not null)
            {
                if (!isSameAccount)
                {
                    mgr.SetActiveAccount(backendAccount);
                    await SwitchToAccountAsync(backendAccount);

                    _ = CheckPresenceAsync();

                    OnPropertyChanged(nameof(ShouldShowGames));
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_SELECT, $"Account {SelectedAccount.Username} is already active, skipping switch");
                }
            }

            HideChangeAccountDialog();
        }

        [RelayCommand]
        private async Task AddAccount()
        {
            const string LOG_IDENT_ADD = $"{LOG_IDENT}::AddAccount";

            var mgr = GetManager();
            if (mgr == null)
            {
                Frontend.ShowMessageBox("Account manager is not available.", MessageBoxImage.Error);
                return;
            }

            AltAccount? newAccount = null;

            try
            {
                if (string.Equals(SelectedAddMethod, "Quick Sign-In", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.WriteLine(LOG_IDENT_ADD, "Adding account via Quick Sign-In (preferred).");

                    newAccount = await mgr.AddAccountByQuickSignInAsync();

                    if (newAccount is null)
                    {
                        App.Logger.WriteLine(LOG_IDENT_ADD, "Quick Sign-In returned null - no account was added.");
                        Frontend.ShowMessageBox("Quick Sign-In was cancelled or failed. Please try again or use browser login.", MessageBoxImage.Information);
                        return;
                    }
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT_ADD, "Adding account via Browser (explicit selection).");
                    newAccount = await mgr.AddAccountByBrowser();
                }

                if (newAccount is not null)
                {
                    if (!Accounts.Any(a => a.Id == newAccount.UserId))
                    {
                        Accounts.Add(new Account(newAccount.UserId, newAccount.DisplayName, newAccount.Username, ""));

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string? avatarUrl = await GetAvatarUrl(newAccount.UserId);
                                var updatedAccount = new Account(newAccount.UserId, newAccount.DisplayName, newAccount.Username, avatarUrl);

                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    var existingAccount = Accounts.FirstOrDefault(a => a.Id == newAccount.UserId);
                                    if (existingAccount != null)
                                    {
                                        var index = Accounts.IndexOf(existingAccount);
                                        Accounts[index] = updatedAccount;
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteLine($"{LOG_IDENT_ADD}::AvatarLoad", $"Exception: {ex.Message}");
                            }
                        });
                    }

                    HideChangeAccountDialog();

                    mgr.SetActiveAccount(newAccount);
                    await SwitchToAccountAsync(newAccount);

                    _ = CheckPresenceAsync();

                    OnPropertyChanged(nameof(ShouldShowGames));
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_ADD, $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to add account: {ex.Message}", MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteAccount(Account? account)
        {
            const string LOG_IDENT_DELETE = $"{LOG_IDENT}::DeleteAccount";

            var mgr = GetManager();
            if (mgr == null)
            {
                Frontend.ShowMessageBox("Account manager is not available.", MessageBoxImage.Error);
                return;
            }

            var target = account ?? SelectedAccount;
            if (target is null)
            {
                Frontend.ShowMessageBox("Please select an account to delete.", MessageBoxImage.Warning);
                return;
            }

            var backendAccount = mgr.Accounts.FirstOrDefault(acc => acc.UserId == target.Id);
            if (backendAccount is null)
            {
                Frontend.ShowMessageBox("Selected account could not be found in the backend.", MessageBoxImage.Error);
                return;
            }

            var result = Frontend.ShowMessageBox(
            $"Delete account '{target.DisplayName}' (@{target.Username})?",
            MessageBoxImage.Warning,
            MessageBoxButton.YesNo
            );
            if (result != MessageBoxResult.Yes) return;

            bool isDeletingActiveAccount = mgr.ActiveAccount?.UserId == target.Id;

            var otherAccounts = mgr.Accounts.Where(acc => acc.UserId != target.Id).ToList();

            bool removed = mgr.RemoveAccount(backendAccount);
            if (!removed)
            {
                Frontend.ShowMessageBox("Failed to delete account.", MessageBoxImage.Error);
                return;
            }

            var uiAccount = Accounts.FirstOrDefault(a => a.Id == target.Id);
            if (uiAccount != null) Accounts.Remove(uiAccount);

            if (isDeletingActiveAccount && otherAccounts.Any())
            {
                var newActiveAccount = otherAccounts.LastOrDefault() ?? otherAccounts.First();

                mgr.SetActiveAccount(newActiveAccount);
                await SwitchToAccountAsync(newActiveAccount);

                App.Logger.WriteLine(LOG_IDENT_DELETE, $"Automatically switched to account: {newActiveAccount.Username}");
            }
            else if (isDeletingActiveAccount)
            {
                mgr.SetActiveAccount(null);
                CurrentUserDisplayName = "Not Logged In";
                CurrentUserUsername = "";
                CurrentUserAvatarUrl = "";
                CurrentUserAvatar = null;

                FriendsCount = 0;
                FollowersCount = 0;
                FollowingCount = 0;
                IsAccountInformationVisible = false;

                DiscoveryGames.Clear();
                FavoriteGames.Clear();
                ContinuePlayingGames.Clear();
                SearchResults.Clear();

                Friends.Clear();

                App.Logger.WriteLine(LOG_IDENT_DELETE, "No accounts remaining after deletion");
            }

            var currentActiveAccount = mgr.ActiveAccount;
            if (currentActiveAccount != null)
            {
                SelectedAccount = Accounts.FirstOrDefault(a => a.Id == currentActiveAccount.UserId);
            }
            else
            {
                SelectedAccount = null;
            }

            OnPropertyChanged(nameof(ShouldShowGames));

            App.Logger.WriteLine(LOG_IDENT_DELETE, $"Account '{target.DisplayName}' deleted successfully");
        }

        [RelayCommand]
        private async Task LaunchSubplace(long placeId)
        {
            const string LOG_IDENT_SUBPLACE = $"{LOG_IDENT}::LaunchSubplace";

            var mgr = GetManager();
            if (mgr?.ActiveAccount is null)
            {
                Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            try
            {
                PlaceId = placeId.ToString();
                mgr.SetCurrentPlaceId(PlaceId);
                mgr.SetCurrentServerInstanceId(ServerId);

                await mgr.LaunchAccountToPlaceAsync(mgr.ActiveAccount, placeId, ServerId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_SUBPLACE, $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to launch subplace: {ex.Message}", MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task LaunchRoblox()
        {
            const string LOG_IDENT_LAUNCH = $"{LOG_IDENT}::LaunchRoblox";

            var mgr = GetManager();
            if (mgr?.ActiveAccount is null)
            {
                Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PlaceId))
            {
                Frontend.ShowMessageBox("Please enter a Place ID.", MessageBoxImage.Warning);
                return;
            }

            if (!long.TryParse(PlaceId, out long placeId))
            {
                Frontend.ShowMessageBox("Please enter a valid Place ID.", MessageBoxImage.Warning);
                return;
            }

            PlaceId = placeId.ToString();
            mgr.SetCurrentPlaceId(PlaceId);
            mgr.SetCurrentServerInstanceId(ServerId);

            await mgr.LaunchAccountToPlaceAsync(mgr.ActiveAccount, placeId, ServerId);
        }

        [RelayCommand]
        private async Task JoinFriend(FriendInfo friend)
        {
            const string LOG_IDENT_JOIN_FRIEND = $"{LOG_IDENT}::JoinFriend";

            try
            {
                if (friend == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_JOIN_FRIEND, "Friend parameter is null");
                    Frontend.ShowMessageBox("Invalid friend information.", MessageBoxImage.Warning);
                    return;
                }

                var mgr = GetManager();
                if (mgr?.ActiveAccount is null)
                {
                    Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                    return;
                }

                if (friend.PresenceType != 2)
                {
                    Frontend.ShowMessageBox($"{friend.DisplayName} is not currently in a game.", MessageBoxImage.Information);
                    return;
                }

                var presenceData = await FetchPresenceForUsersAsync(mgr.ActiveAccount.UserId, new List<long> { friend.Id }, CancellationToken.None);

                if (presenceData == null || !presenceData.TryGetValue(friend.Id, out var friendPresence) || friendPresence == null)
                {
                    Frontend.ShowMessageBox($"Unable to get game information for {friend.DisplayName}.", MessageBoxImage.Warning);
                    return;
                }

                string? gameInstanceId = friendPresence.GameId;
                long? placeId = friendPresence.PlaceId ?? friendPresence.RootPlaceId;

                if (!placeId.HasValue || placeId.Value == 0)
                {
                    Frontend.ShowMessageBox($"Unable to determine the game {friend.DisplayName} is playing.", MessageBoxImage.Warning);
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_JOIN_FRIEND, $"Joining friend {friend.DisplayName} in place {placeId}, instance {gameInstanceId}");

                if (string.IsNullOrEmpty(gameInstanceId))
                {
                    Frontend.ShowMessageBox($"Unable to get server information for {friend.DisplayName}'s game.", MessageBoxImage.Warning);
                    return;
                }

                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowLoading($"Joining {friend.DisplayName}'s game...");

                try
                {
                    await mgr.LaunchAccountToPlaceAsync(mgr.ActiveAccount, placeId.Value, gameInstanceId);

                    App.Logger.WriteLine(LOG_IDENT_JOIN_FRIEND, $"Successfully launched game to join {friend.DisplayName} in instance {gameInstanceId}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_JOIN_FRIEND, $"Exception during launch: {ex.Message}");
                    Frontend.ShowMessageBox($"Failed to join {friend.DisplayName}'s game: {ex.Message}", MessageBoxImage.Error);
                }
                finally
                {
                    mainWindow?.HideLoading();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_JOIN_FRIEND, $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to join {friend?.DisplayName ?? "friend"}: {ex.Message}", MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private Task LaunchDiscoveryPageGame(long placeId)
            => LaunchGameAsync(placeId);

        [RelayCommand]
        private Task AutoFindAndJoinServer(long placeId)
            => AutoFindAndJoinGameAsync(placeId);

        [RelayCommand]
        private Task LaunchFavoriteGame(long placeId)
            => LaunchGameAsync(placeId);

        [RelayCommand]
        private Task AutoFindAndJoinFavoriteGame(long placeId)
            => AutoFindAndJoinGameAsync(placeId);

        [RelayCommand]
        private Task LaunchContinuePlayingGame(long placeId)
            => LaunchGameAsync(placeId);

        [RelayCommand]
        private Task AutoFindAndJoinContinuePlayingGame(long placeId)
            => AutoFindAndJoinGameAsync(placeId);

        [RelayCommand]
        private void StartDrag(Account? account)
        {
            if (account is null) return;
            DraggedAccount = account;
        }

        [RelayCommand]
        private void EndDrag()
        {
            DraggedAccount = null;
        }

        [RelayCommand]
        private void DropOnAccount(Account? targetAccount)
        {
            if (DraggedAccount is null || targetAccount is null || DraggedAccount == targetAccount)
                return;

            var mgr = GetManager();
            if (mgr is null) return;

            int oldIndex = Accounts.IndexOf(DraggedAccount);
            int newIndex = Accounts.IndexOf(targetAccount);

            if (oldIndex != -1 && newIndex != -1)
            {
                Accounts.Move(oldIndex, newIndex);
                UpdateBackendAccountOrder();
            }

            DraggedAccount = null;
        }

        [RelayCommand]
        private void PersistPlaceId()
        {
            var mgr = GetManager();
            if (mgr != null)
            {
                mgr.SetCurrentPlaceId(PlaceId);
            }
        }

        [RelayCommand]
        private void PersistServerId()
        {
            var mgr = GetManager();
            if (mgr != null)
            {
                mgr.SetCurrentServerInstanceId(ServerId);
            }
        }

        [RelayCommand]
        private Task RefreshDiscoveryGames()
            => ExecuteWithCancellationSupport(
                token => FetchDiscoveryPageGamesAsync(GetManager()?.ActiveAccount?.UserId ?? 0, token),
                new CancellationTokenSource(),
                "RefreshDiscoveryGames");

        [RelayCommand]
        private Task RefreshFavoriteGames()
            => ExecuteWithCancellationSupport(
                token => FetchFavoriteGamesAsync(GetManager()?.ActiveAccount?.UserId ?? 0, token),
                new CancellationTokenSource(),
                "RefreshFavoriteGames");

        [RelayCommand]
        private Task RefreshContinuePlaying()
            => ExecuteWithCancellationSupport(
                token => FetchContinuePlayingGamesAsync(token),
                new CancellationTokenSource(),
                "RefreshContinuePlaying");

        [RelayCommand]
        private Task RefreshFriends()
            => ExecuteWithCancellationSupport(
                token => FetchFriendsAsync(GetManager()?.ActiveAccount?.UserId ?? 0, token),
                new CancellationTokenSource(),
                "RefreshFriends");

        [RelayCommand]
        private async Task ShowPrivateServers()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() => IsPrivateServersModalOpen = true);
                await PopulatePrivateServersAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::ShowPrivateServers", $"Exception: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() => IsPrivateServersModalOpen = false);
            }
        }

        [RelayCommand]
        private void HidePrivateServers()
        {
            Application.Current?.Dispatcher?.Invoke(() => IsPrivateServersModalOpen = false);
        }

        [RelayCommand]
        private async Task JoinPrivateServer(string accessCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accessCode))
                    return;

                if (!long.TryParse(PlaceId, out long placeId) || placeId == 0)
                {
                    Frontend.ShowMessageBox("Please select a game first (set Place ID).", MessageBoxImage.Warning);
                    return;
                }

                var uri = $"roblox://experiences/start?placeId={placeId}&accessCode={Uri.EscapeDataString(accessCode)}";

                try
                {
                    var psi = new ProcessStartInfo(uri) { UseShellExecute = true };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine($"{LOG_IDENT}::JoinPrivateServer::LaunchUri", $"Failed to launch roblox uri: {ex.Message}");
                    Frontend.ShowMessageBox("Failed to launch Roblox. Make sure Roblox is installed and the roblox:// protocol is registered.", MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::JoinPrivateServer", $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to join server: {ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                Application.Current?.Dispatcher?.Invoke(() => IsPrivateServersModalOpen = false);
            }
        }

        private async Task PopulatePrivateServersAsync()
        {
            const string LOG_IDENT_PRIVATE = $"{LOG_IDENT}::PopulatePrivateServers";

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                PrivateServers.Clear();
                ArePrivateServersEmpty = false;
            });

            if (!long.TryParse(PlaceId, out long placeId) || placeId == 0)
            {
                App.Logger.WriteLine(LOG_IDENT_PRIVATE, "PlaceId invalid or not set; cannot populate private servers.");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PrivateServers.Clear();
                    ArePrivateServersEmpty = true;
                });
                return;
            }

            try
            {
                string url = $"https://games.roblox.com/v1/games/{placeId}/private-servers?excludeFriendServers=false&sortOrder=Asc";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                var mgr = GetManager();
                if (mgr?.ActiveAccount != null)
                {
                    string? cookie = mgr.GetRoblosecurityForUser(mgr.ActiveAccount.UserId);
                    if (!string.IsNullOrEmpty(cookie))
                    {
                        req.Headers.Add("Origin", "https://www.roblox.com");
                        req.Headers.Add("Referrer", "https://www.roblox.com");
                        req.Headers.Remove("Cookie");
                        req.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
                    }
                }

                using var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT_PRIVATE, $"Private servers request failed: {(int)resp.StatusCode}");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        PrivateServers.Clear();
                        ArePrivateServersEmpty = true;
                    });
                    return;
                }

                var body = await resp.Content.ReadAsStringAsync();
                var jo = JObject.Parse(body);
                var data = jo["data"] as JArray;

                if (data == null || !data.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT_PRIVATE, "No private servers returned by API.");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        PrivateServers.Clear();
                        ArePrivateServersEmpty = true;
                    });
                    return;
                }

                var tempList = new List<(long VipServerId, string AccessCode, string Name, long OwnerId, string OwnerName, int MaxPlayers, int CurrentPlayers)>();
                var ownerIds = new HashSet<long>();

                foreach (var srv in data)
                {
                    try
                    {
                        int maxPlayers = srv["maxPlayers"]?.Value<int>() ?? 0;
                        var playersArray = srv["players"] as JArray;
                        int currentPlayers = playersArray?.Count ?? 0;
                        string name = srv["name"]?.Value<string>() ?? "";
                        long vipServerId = srv["vipServerId"]?.Value<long>() ?? 0;
                        string accessCode = srv["accessCode"]?.Value<string>() ?? vipServerId.ToString();

                        var owner = srv["owner"];
                        long ownerId = owner?["id"]?.Value<long>() ?? 0;
                        string ownerName = owner?["name"]?.Value<string>() ?? "";

                        tempList.Add((vipServerId, accessCode, name, ownerId, ownerName, maxPlayers, currentPlayers));

                        if (ownerId != 0)
                            ownerIds.Add(ownerId);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine($"{LOG_IDENT_PRIVATE}::ParseItem", $"Exception parsing server item: {ex.Message}");
                    }
                }

                // removed "optimizations" because I am an idiot
                var avatarUrls = new Dictionary<long, string?>();
                if (ownerIds.Any())
                {
                    try
                    {
                        foreach (var id in ownerIds)
                            avatarUrls[id] = null;

                        var tasks = ownerIds.Select(async id =>
                        {
                            try
                            {
                                var img = await GetAvatarUrl(id).ConfigureAwait(false);
                                lock (avatarUrls)
                                {
                                    avatarUrls[id] = img;
                                }
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteLine($"{LOG_IDENT_PRIVATE}::GetAvatarUrl", $"Failed to fetch avatar for {id}: {ex.Message}");
                                lock (avatarUrls)
                                {
                                    avatarUrls[id] = null;
                                }
                            }
                        }).ToArray();

                        await Task.WhenAll(tasks).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine($"{LOG_IDENT_PRIVATE}::AvatarFetch", $"Failed fetching avatars individually: {ex.Message}");
                        foreach (var id in ownerIds)
                            if (!avatarUrls.ContainsKey(id))
                                avatarUrls[id] = null;
                    }
                }

                var list = new List<PrivateServerInfo>();
                foreach (var t in tempList)
                {
                    string? ownerAvatar = null;
                    if (t.OwnerId != 0 && avatarUrls.TryGetValue(t.OwnerId, out var au))
                        ownerAvatar = au;

                    var entry = new PrivateServerInfo(
                        t.VipServerId,
                        t.AccessCode,
                        t.Name,
                        t.OwnerId,
                        t.OwnerName,
                        ownerAvatar,
                        t.MaxPlayers,
                        t.CurrentPlayers
                    );

                    list.Add(entry);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PrivateServers.Clear();
                    foreach (var p in list)
                        PrivateServers.Add(p);

                    ArePrivateServersEmpty = list.Count == 0;
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_PRIVATE, $"Exception: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PrivateServers.Clear();
                    ArePrivateServersEmpty = true;
                });
            }
        }
    }
}