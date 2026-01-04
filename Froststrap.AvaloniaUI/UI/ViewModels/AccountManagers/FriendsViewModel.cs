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

using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace Froststrap.UI.ViewModels.AccountManagers
{
    public record FriendInfo(long Id, string DisplayName, string? AvatarUrl, int PresenceType, string LastLocation, string StatusColor, string PlayingGameName)
    {
        public bool IsOnline => PresenceType == 2;
    }

    public class FriendData
    {
        public long Id { get; set; }
        public string DisplayName { get; set; } = "";
        public bool IsOnline { get; set; }
        public int PresenceType { get; set; }
    }

    public partial class FriendsViewModel : ObservableObject
    {
        private const string LOG_IDENT = "FriendsViewModel";

        [ObservableProperty]
        private ObservableCollection<string> _friendFilters = new(new[] { "All", "Studio", "Online", "Website", "Offline" });

        [ObservableProperty]
        private string _selectedFriendFilter = "All";

        [ObservableProperty]
        private ObservableCollection<FriendInfo> _filteredFriends = new();

        [ObservableProperty]
        private ObservableCollection<FriendInfo> _friends = new();

        [ObservableProperty]
        private bool _isPresenceLoading = false;

        [ObservableProperty]
        private string _presenceStatus = "";

        [ObservableProperty]
        private bool _hasApiFriends = false;

        [ObservableProperty]
        private bool _hasFriends = false;

        private long _lastActiveUserId = 0;

        private AccountManager Manager => AccountManager.Shared;

        private CancellationTokenSource? _friendsRefreshCts;
        private System.Timers.Timer? _presenceUpdateTimer;

        private Elements.AccountManagers.MainWindow? GetMainWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is Elements.AccountManagers.MainWindow accountManagerWindow)
                {
                    return accountManagerWindow;
                }
            }
            return null;
        }

        public FriendsViewModel()
        {
            if (Design.IsDesignMode)
                return;

            AccountManager.Shared.ActiveAccountChanged += OnActiveAccountChanged;

            _ = RefreshFriends();
            InitializePresenceTimer();
        }

        private async void OnActiveAccountChanged(AltAccount? newAccount)
        {
            if (newAccount == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Friends.Clear();
                    FilteredFriends.Clear();
                    PresenceStatus = "No active account";
                    HasApiFriends = false;
                    HasFriends = false;
                });
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PresenceStatus = "Refreshing friends...";
                IsPresenceLoading = true;
            });

            try
            {
                await RefreshFriends();
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsPresenceLoading = false;
                });
            }
        }

        private void InitializePresenceTimer()
        {
            _presenceUpdateTimer = new System.Timers.Timer(20000);
            _presenceUpdateTimer.Elapsed += OnPresenceTimerElapsed;
            _presenceUpdateTimer.AutoReset = true;
            _presenceUpdateTimer.Start();
        }

        private async void OnPresenceTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (IsPresenceLoading)
                return;

            _ = CheckForAccountChangeAsync();
            _ = CheckPresenceAsync();
        }

        private async Task CheckPresenceAsync()
        {
            const string LOG_IDENT_PRESENCE = $"{LOG_IDENT}::CheckPresence";

            var activeUserId = AccountsViewModel.GetActiveUserId();
            if (activeUserId == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Friends.Clear();
                    FilteredFriends.Clear();
                    PresenceStatus = "No active account";
                });
                return;
            }

            if (_lastActiveUserId != activeUserId.Value)
            {
                _lastActiveUserId = activeUserId.Value;
                await RefreshFriends();
                return;
            }

            if (IsPresenceLoading)
                return;

            try
            {
                IsPresenceLoading = true;

                // Get ALL friend IDs for presence checking
                List<long> friendIds = new List<long>();
                if (Friends != null && Friends.Any())
                {
                    friendIds = Friends.Select(f => f.Id).ToList();
                }

                var ids = new List<long> { activeUserId.Value };
                ids.AddRange(friendIds.Where(id => id != activeUserId.Value));
                ids = ids.Distinct().ToList();

                Dictionary<long, UserPresence>? presenceData = null;
                if (ids.Any())
                {
                    presenceData = await FetchPresenceForUsersAsync(activeUserId.Value, ids, CancellationToken.None);
                }

                if (presenceData == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_PRESENCE, "Presence API returned null");
                    return;
                }

                var presenceMap = presenceData;

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

                            string statusColor = GetStatusColor(presenceType);
                            string displayLocation = GetDisplayLocation(presenceType, playingGameName, lastLocation);

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

                    await Dispatcher.UIThread.InvokeAsync(() =>
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
            }
            finally
            {
                IsPresenceLoading = false;
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

        private async Task<PlaceDetails?> FetchPlaceDetailsAsync(long placeId)
        {
            const string LOG_IDENT_PLACE_DETAILS = $"{LOG_IDENT}::FetchPlaceDetails";

            try
            {
                using var client = new HttpClient();
                string url = $"https://games.roblox.com/v1/games/multiget-place-details?placeIds={placeId}";

                var activeAccount = Manager.ActiveAccount;
                if (activeAccount != null)
                {
                    string? cookie = Manager.GetRoblosecurityForUser(activeAccount.UserId);
                    if (!string.IsNullOrEmpty(cookie))
                    {
                        client.DefaultRequestHeaders.Add("Cookie", $".ROBLOSECURITY={cookie}");
                    }
                }

                var response = await client.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
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

        [RelayCommand]
        private async Task RefreshFriends()
        {
            var activeUserId = AccountsViewModel.GetActiveUserId();
            if (activeUserId == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Friends.Clear();
                    FilteredFriends.Clear();
                    PresenceStatus = "No active account";
                });
                return;
            }

            await ExecuteWithCancellationSupport(
                token => FetchFriendsAsync(activeUserId.Value, token),
                "RefreshFriends");
        }

        [RelayCommand]
        private async Task JoinFriend(FriendInfo friend)
        {
            const string LOG_IDENT_JOIN_FRIEND = $"{LOG_IDENT}::JoinFriend";

            try
            {
                if (_friendsRefreshCts != null && !_friendsRefreshCts.IsCancellationRequested)
                {
                    _friendsRefreshCts.Cancel();
                    _friendsRefreshCts.Dispose();
                    _friendsRefreshCts = null;
                }

                await Task.Delay(50);

                if (friend == null)
                {
                    App.Logger.WriteLine(LOG_IDENT_JOIN_FRIEND, "Friend parameter is null");
                    Frontend.ShowMessageBox("Invalid friend information.", MessageBoxImage.Warning);
                    return;
                }

                var activeAccount = Manager.ActiveAccount;
                if (activeAccount is null)
                {
                    Frontend.ShowMessageBox("Please select an account first.", MessageBoxImage.Warning);
                    return;
                }

                if (friend.PresenceType != 2)
                {
                    Frontend.ShowMessageBox($"{friend.DisplayName} is not currently in a game.", MessageBoxImage.Information);
                    return;
                }

                var presenceData = await FetchPresenceForUsersAsync(activeAccount.UserId, new List<long> { friend.Id }, CancellationToken.None);

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

                try
                {
                    await Manager.LaunchAccountToPlaceAsync(activeAccount, placeId.Value, gameInstanceId);

                    App.Logger.WriteLine(LOG_IDENT_JOIN_FRIEND, $"Successfully launched game to join {friend.DisplayName} in instance {gameInstanceId}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_JOIN_FRIEND, $"Exception during launch: {ex.Message}");
                    Frontend.ShowMessageBox($"Failed to join {friend.DisplayName}'s game: {ex.Message}", MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_JOIN_FRIEND, $"Exception: {ex.Message}");
                Frontend.ShowMessageBox($"Failed to join {friend?.DisplayName ?? "friend"}: {ex.Message}", MessageBoxImage.Error);
            }
        }

        private async Task ExecuteWithCancellationSupport(Func<CancellationToken, Task> action, string operationName)
        {
            if (_friendsRefreshCts != null && !_friendsRefreshCts.IsCancellationRequested)
            {
                _friendsRefreshCts.Cancel();
                await Task.Delay(100);
            }

            _friendsRefreshCts?.Dispose();

            _friendsRefreshCts = new CancellationTokenSource();
            var token = _friendsRefreshCts.Token;

            try
            {
                await action(token);
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::{operationName}", "Cancelled.");
            }
            catch (ObjectDisposedException)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::{operationName}", "Token was disposed.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::{operationName}", $"Exception: {ex.Message}");
            }
        }

        private async Task FetchFriendsAsync(long userId, CancellationToken token = default)
        {
            const string LOG_IDENT_FRIENDS = $"{LOG_IDENT}::FetchFriends";
            var mainWindow = GetMainWindow();

            try
            {
                mainWindow?.ShowLoading("Loading friends...");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Friends.Clear();
                    FilteredFriends.Clear();
                    HasApiFriends = false;
                    HasFriends = false;
                    PresenceStatus = "Loading friends...";
                    IsPresenceLoading = true;
                });

                if (userId == 0)
                {
                    App.Logger.WriteLine(LOG_IDENT_FRIENDS, "UserId is 0, skipping friends fetch");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT_FRIENDS, $"Starting friends fetch for user {userId}");

                var friendsData = await FetchFriendsListAsync(userId, token);

                if (!friendsData.Any())
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        PresenceStatus = "No friends found";
                        HasApiFriends = false;
                        HasFriends = false;
                    });
                    return;
                }

                HasApiFriends = true;

                var allFriendIds = friendsData.Select(f => f.Id).ToList();

                Dictionary<long, UserPresence> presenceMap = new();
                if (allFriendIds.Any())
                {
                    presenceMap = await FetchPresenceForUsersAsync(userId, allFriendIds, token);
                }

                var avatarUrls = await GetAvatarUrlsBulkAsync(allFriendIds, token);

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

                    avatarUrls.TryGetValue(friend.Id, out var avatarUrl);

                    int presenceType = 0;
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

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Friends.Clear();
                    foreach (var f in orderedFriends)
                        Friends.Add(f);

                    FilterFriends();
                    PresenceStatus = $"Loaded {friendsData.Count} friends";
                });

                App.Logger.WriteLine(LOG_IDENT_FRIENDS, $"Successfully loaded {friendsData.Count} friends for user {userId}");
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT_FRIENDS, "Friends fetch was cancelled");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PresenceStatus = "Loading cancelled";
                    HasApiFriends = false;
                    HasFriends = false;
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::FetchFriends", $"Exception: {ex.Message}");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PresenceStatus = "Failed to load friends";
                    HasApiFriends = false;
                    HasFriends = false;
                });
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsPresenceLoading = false;
                });

                mainWindow?.HideLoading();
            }
        }

        private async Task<Dictionary<long, string?>> GetAvatarUrlsBulkAsync(List<long> userIds, CancellationToken token = default)
        {
            var result = new Dictionary<long, string?>();
            if (userIds == null || userIds.Count == 0) return result;

            const int batchSize = 100;
            try
            {
                for (int i = 0; i < userIds.Count; i += batchSize)
                {
                    token.ThrowIfCancellationRequested();

                    var batch = userIds.Skip(i).Take(batchSize).ToList();
                    foreach (var id in batch) if (!result.ContainsKey(id)) result[id] = null;

                    string idsParam = string.Join(',', batch);
                    string url = $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={idsParam}&size=75x75&format=Png&isCircular=true";

                    using var req = new HttpRequestMessage(HttpMethod.Get, url);

                    var activeAccount = Manager.ActiveAccount;
                    if (activeAccount != null)
                    {
                        string? cookie = Manager.GetRoblosecurityForUser(activeAccount.UserId);
                        if (!string.IsNullOrWhiteSpace(cookie))
                            req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
                    }

                    using var client = new HttpClient();
                    using var resp = await client.SendAsync(req, token).ConfigureAwait(false);

                    if (!resp.IsSuccessStatusCode)
                    {
                        App.Logger.WriteLine($"{LOG_IDENT}::GetAvatarUrlsBulkAsync", $"Thumbnail batch request failed: {(int)resp.StatusCode}");
                        continue;
                    }

                    var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);

                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in dataElem.EnumerateArray())
                            {
                                if (item.TryGetProperty("targetId", out var tidElem) && tidElem.ValueKind == JsonValueKind.Number
                                    && item.TryGetProperty("imageUrl", out var imgElem) && imgElem.ValueKind == JsonValueKind.String)
                                {
                                    long tid = tidElem.GetInt64();
                                    string? img = imgElem.GetString();
                                    result[tid] = string.IsNullOrWhiteSpace(img) ? null : img;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine($"{LOG_IDENT}::GetAvatarUrlsBulkAsync", $"Failed parsing thumbnail response: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                App.Logger.WriteLine($"{LOG_IDENT}::GetAvatarUrlsBulkAsync", $"Exception: {ex.Message}");
                foreach (var id in userIds)
                    if (!result.ContainsKey(id))
                        result[id] = null;
            }

            return result;
        }

        private async Task<List<FriendData>> FetchFriendsListAsync(long userId, CancellationToken token)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                string url = $"https://friends.roblox.com/v1/users/{userId}/friends";

                App.Logger.WriteLine("FetchFriendsListAsync", $"Fetching friends from: {url}");

                var response = await client.GetAsync(url, token);

                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine("FetchFriendsListAsync", $"Failed to fetch friends: {response.StatusCode}");
                    return new List<FriendData>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var data = json["data"] as JArray;

                if (data == null)
                {
                    App.Logger.WriteLine("FetchFriendsListAsync", "No friends data found in response (data is null)");
                    return new List<FriendData>();
                }

                App.Logger.WriteLine("FetchFriendsListAsync", $"Raw API response contains {data.Count} friends");

                var friends = new List<FriendData>();
                int processedCount = 0;
                int skippedCount = 0;

                foreach (var friend in data)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        long? id = friend["id"]?.Value<long>();
                        string? name = friend["name"]?.ToString();
                        string? displayName = friend["displayName"]?.ToString();

                        if (id.HasValue && id > 0)
                        {
                            friends.Add(new FriendData
                            {
                                Id = id.Value,
                                DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : name ?? id.Value.ToString(),
                                IsOnline = false,
                                PresenceType = 0
                            });
                            processedCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("FetchFriendsListAsync", $"Error processing friend {friend}: {ex.Message}");
                        skippedCount++;
                    }
                }

                App.Logger.WriteLine("FetchFriendsListAsync", $"Successfully processed {processedCount} friends, skipped {skippedCount}");
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

                string? cookie = Manager.GetRoblosecurityForUser(userId);
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
                App.Logger.WriteLine($"{LOG_IDENT}::FetchPresenceForUsers", $"Exception: {ex.Message}");
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
                HasFriends = false;
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

            HasFriends = FilteredFriends.Any();
        }

        partial void OnSelectedFriendFilterChanged(string value)
        {
            FilterFriends();
        }

        private async Task CheckForAccountChangeAsync()
        {
            var activeUserId = AccountsViewModel.GetActiveUserId();

            if (activeUserId == null)
            {
                if (_lastActiveUserId != 0)
                {
                    _lastActiveUserId = 0;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Friends.Clear();
                        FilteredFriends.Clear();
                        PresenceStatus = "No active account";
                    });
                }
                return;
            }

            if (_lastActiveUserId != activeUserId.Value)
            {
                _lastActiveUserId = activeUserId.Value;

                await ExecuteWithCancellationSupport(
                    token => FetchFriendsAsync(activeUserId.Value, token),
                    "AccountChangeRefresh");

                PresenceStatus = "Refreshing friends...";
            }
        }
    }
}