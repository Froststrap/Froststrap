// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.APIs.RealtimeMessaging;
using Froststrap.Models.APIs.Roblox;
using Froststrap.Models.Overlay;
using Froststrap.RobloxInterfaces;
using System.Windows.Input;

using PeopleApi = Froststrap.RobloxInterfaces.People;

namespace Froststrap.UI.ViewModels.Overlay.SearchTabs
{
    internal class PeopleBrowserViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);
        private static readonly TimeSpan PendingRefresh = TimeSpan.FromSeconds(45);
        private const int FriendLimit = 100;

        private readonly DispatcherTimer _debounce;
        private readonly DispatcherTimer _pendingTimer;
        private readonly CancellationTokenSource _disposeCts = new();
        private readonly HashSet<long> _pendingIds = [];
        private readonly List<SearchPerson> _friendsCache = [];
        private int _searchGeneration;
        private bool _disposed;

        private string _query = String.Empty;
        private bool _showingFriends;
        private bool _friendsLoaded;
        private bool _loadingFriends;
        private HashSet<long> _friendIds = [];
        private long _me;

        public BulkObservableCollection<SearchPerson> People { get; } = [];

        public ICommand JoinCommand { get; }
        public ICommand ViewGameCommand { get; }
        public ICommand AddFriendCommand { get; }

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value)
                    return;

                _selectedIndex = value;
                OnPropertyChanged(nameof(SelectedIndex));
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                Refreshed();
            }
        }

        public bool ShowPeople => People.Count > 0;
        public bool ShowEmpty => !IsBusy && People.Count == 0;
        public bool ShowHeader => _showingFriends && People.Count > 0;

        public static string HeaderText => Strings.Menu_Overlay_SearchBar_Friends;

        public string EmptyText
        {
            get
            {
                if (_me == 0)
                    return Strings.Menu_Overlay_SearchBar_NeedsCookies;

                if (String.IsNullOrWhiteSpace(_query))
                    return _loadingFriends
                        ? Strings.Menu_Overlay_SearchBar_LoadingPeople
                        : Strings.Menu_Overlay_SearchBar_NoFriends;

                return String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_SearchBar_NoUsers, _query.Trim());
            }
        }

        public PeopleBrowserViewModel()
        {
            _debounce = new DispatcherTimer { Interval = SearchDelay };
            _debounce.Tick += async (_, _) =>
            {
                _debounce.Stop();
                await SearchUsersAsync(_query);
            };

            _pendingTimer = new DispatcherTimer { Interval = PendingRefresh };
            _pendingTimer.Tick += async (_, _) => await RefreshPendingAsync();

            JoinCommand = new AsyncRelayCommand<SearchPerson>(JoinAsync);
            ViewGameCommand = new RelayCommand<SearchPerson>(ViewGame);
            AddFriendCommand = new AsyncRelayCommand<SearchPerson>(AddFriendAsync);

            App.Cookies.WatchAccount(this, static vm => vm.OnAccountChanged());

            RealtimeMessaging.AnyFriendshipChanged += OnFriendshipChanged;
        }

        public void Prewarm()
        {
            if (!_friendsLoaded && !_loadingFriends)
                _ = LoadFriendsAsync();
        }

        public void SetQuery(string query)
        {
            string next = query ?? String.Empty;
            bool same = _query == next;

            _query = next;

            if (String.IsNullOrWhiteSpace(next))
            {
                _debounce.Stop();
                _searchGeneration++;
                _showingFriends = true;
                ShowCachedFriends();
                return;
            }

            if (same && People.Count > 0 && !_showingFriends)
                return;

            _debounce.Stop();
            _searchGeneration++;
            _showingFriends = false;
            _debounce.Start();
        }

        public void MoveSelection(int delta)
        {
            if (People.Count == 0)
                return;

            int next = SelectedIndex + delta;

            if (next < 0)
                next = People.Count - 1;
            else if (next >= People.Count)
                next = 0;

            SelectedIndex = next;
        }

        public void JoinSelected()
        {
            if (SelectedIndex < 0 || SelectedIndex >= People.Count)
                return;

            SearchPerson person = People[SelectedIndex];

            if (person.CanJoin)
                _ = JoinAsync(person);
            else if (person.CanViewGame)
                ViewGame(person);
        }

        private void ShowCachedFriends()
        {
            if (_friendsCache.Count == 0)
            {
                if (!_friendsLoaded && !_loadingFriends)
                    _ = LoadFriendsAsync();

                return;
            }

            People.ReplaceAll(_friendsCache);
            SelectedIndex = _friendsCache.Count > 0 ? 0 : -1;
            Refreshed();
        }

        private void OnAccountChanged() => Dispatcher.UIThread.Post(() =>
        {
            _friendsLoaded = false;
            _loadingFriends = false;
            _pendingIds.Clear();
            _friendIds = [];
            _friendsCache.Clear();
            _me = 0;

            People.ReplaceAll([]);
            SelectedIndex = -1;
            IsBusy = false;
            _pendingTimer.Stop();
            Refreshed();

            if (_showingFriends && String.IsNullOrWhiteSpace(_query))
                _ = LoadFriendsAsync();
        });

        private async Task LoadFriendsAsync()
        {
            if (_loadingFriends)
                return;

            _loadingFriends = true;
            IsBusy = true;
            Refreshed();

            try
            {
                var me = App.Cookies.CurrentUser;

                if (me is null)
                {
                    People.ReplaceAll([]);
                    SelectedIndex = -1;
                    return;
                }

                _me = me.Id;

                List<long> ids = await PeopleApi.FetchFriendIdsLimitedAsync(me.Id, FriendLimit, _disposeCts.Token);

                _friendIds = [.. ids];

                if (ids.Count == 0)
                {
                    People.ReplaceAll([]);
                    _friendsCache.Clear();
                    SelectedIndex = -1;
                    _friendsLoaded = true;
                    return;
                }

                var detailsTask   = PeopleApi.FetchDetailsAsync(ids, _disposeCts.Token);
                var presenceTask  = PeopleApi.FetchPresenceAsync(ids, _disposeCts.Token);
                var headshotsTask = PeopleApi.FetchHeadshotsAsync(ids, _disposeCts.Token);

                await Task.WhenAll(detailsTask, presenceTask, headshotsTask);

                Dictionary<long, UserDetails> details   = await detailsTask;
                Dictionary<long, UserPresence> presence = await presenceTask;
                Dictionary<long, string> headshots      = await headshotsTask;

                var people = ids
                    .Select(id => ToPerson(
                        id,
                        details.GetValueOrDefault(id),
                        presence.GetValueOrDefault(id),
                        headshots.GetValueOrDefault(id),
                        isFriend: true))
                    .OrderBy(p => p.Status == UserPresenceType.Offline)
                    .ThenByDescending(p => (int)p.Status)
                    .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                _friendsCache.Clear();
                _friendsCache.AddRange(people);

                if (_showingFriends && String.IsNullOrWhiteSpace(_query))
                {
                    People.ReplaceAll(people);
                    SelectedIndex = people.Count > 0 ? 0 : -1;
                }

                _friendsLoaded = true;

                PrunePending(ids);
            }
            catch (Exception ex)
            {
                App.Logger.Error("People browser: failed to load friends");
                App.Logger.Error(ex);
            }
            finally
            {
                _loadingFriends = false;
                IsBusy = false;
                Refreshed();
                UpdatePendingTimer();
            }
        }

        private void PrunePending(IEnumerable<long> friendIds)
        {
            var ids = friendIds as HashSet<long> ?? [.. friendIds];

            _pendingIds.RemoveWhere(ids.Contains);
        }

        private async Task RefreshPendingAsync()
        {
            if (_pendingIds.Count == 0 || _me == 0)
            {
                _pendingTimer.Stop();
                return;
            }

            try
            {
                HashSet<long> ids = await PeopleApi.FetchFriendIdsAsync(_me, _disposeCts.Token);

                if (ids.Count == 0)
                    return;

                var accepted = _pendingIds.Where(ids.Contains).ToList();

                if (accepted.Count == 0)
                    return;

                foreach (long id in accepted)
                {
                    _pendingIds.Remove(id);
                    _friendIds.Add(id);
                }

                foreach (SearchPerson person in People.Where(p => accepted.Contains(p.UserId)))
                {
                    person.IsFriend = true;
                    person.IsPending = false;
                    person.HasIncoming = false;
                }

                _friendsLoaded = false;
                _friendsCache.Clear();

                UpdatePendingTimer();
            }
            catch (Exception ex)
            {
                App.Logger.Warn("People browser: pending refresh failed");
                App.Logger.Error(ex);
            }
        }

        private void UpdatePendingTimer()
        {
            if (_pendingIds.Count > 0)
                _pendingTimer.Start();
            else
                _pendingTimer.Stop();
        }

        private async Task SearchUsersAsync(string query)
        {
            if (String.IsNullOrWhiteSpace(query))
                return;

            int generation = ++_searchGeneration;

            IsBusy = true;
            Refreshed();

            try
            {
                List<OmniUserSearchContent> users = await PeopleApi.SearchUsersAsync(query, _disposeCts.Token);

                if (generation != _searchGeneration)
                    return;

                if (users.Count == 0)
                {
                    People.ReplaceAll([]);
                    SelectedIndex = -1;
                    return;
                }

                long me = App.Cookies.CurrentUser?.Id ?? 0;
                var ids = users.Select(u => u.ContentId).ToList();

                var detailsTask   = PeopleApi.FetchDetailsAsync(ids, _disposeCts.Token);
                var presenceTask  = PeopleApi.FetchPresenceAsync(ids, _disposeCts.Token);
                var headshotsTask = PeopleApi.FetchHeadshotsAsync(ids, _disposeCts.Token);
                var statusesTask  = PeopleApi.FetchFriendStatusesAsync(me, ids, _disposeCts.Token);

                await Task.WhenAll(detailsTask, presenceTask, headshotsTask, statusesTask);

                if (generation != _searchGeneration)
                    return;

                Dictionary<long, UserDetails> details   = await detailsTask;
                Dictionary<long, UserPresence> presence = await presenceTask;
                Dictionary<long, string> headshots      = await headshotsTask;
                Dictionary<long, FriendStatus> statuses = await statusesTask;

                var people = users
                    .Select(u =>
                    {
                        FriendStatus status = statuses.GetValueOrDefault(u.ContentId, FriendStatus.Unknown);

                        bool verified = details.TryGetValue(u.ContentId, out UserDetails? d)
                            ? d.Data.HasVerifiedBadge
                            : u.HasVerifiedBadge;

                        return new SearchPerson
                        {
                            UserId = u.ContentId,
                            DisplayName = String.IsNullOrEmpty(u.DisplayName) ? u.Username ?? String.Empty : u.DisplayName!,
                            Username = u.Username ?? String.Empty,
                            IsVerified = verified,
                            HeadshotUrl = headshots.GetValueOrDefault(u.ContentId),
                            IsFriend = status == FriendStatus.Friends,
                            IsPending = status == FriendStatus.RequestSent,
                            HasIncoming = status == FriendStatus.RequestReceived,
                            IsSelf = u.ContentId == me,
                            Status = presence.GetValueOrDefault(u.ContentId)?.UserPresenceType ?? UserPresenceType.Offline,
                            GameName = !String.IsNullOrWhiteSpace(presence.GetValueOrDefault(u.ContentId)?.LastLocation)
                                ? presence[u.ContentId].LastLocation
                                : null,
                            PlaceId = presence.GetValueOrDefault(u.ContentId)?.PlaceId ?? 0,
                            RootPlaceId = presence.GetValueOrDefault(u.ContentId)?.RootPlaceId ?? 0,
                            ServerId = String.IsNullOrEmpty(presence.GetValueOrDefault(u.ContentId)?.GameId)
                                ? null
                                : presence[u.ContentId].GameId
                        };
                    })
                    .ToList();

                People.ReplaceAll(people);
                SelectedIndex = people.Count > 0 ? 0 : -1;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (generation != _searchGeneration)
                    return;

                App.Logger.Error("People browser: user search failed");
                App.Logger.Error(ex);
                People.ReplaceAll([]);
                SelectedIndex = -1;
            }
            finally
            {
                if (generation == _searchGeneration)
                {
                    IsBusy = false;
                    Refreshed();
                }
            }
        }

        private static SearchPerson ToPerson(long userId, UserDetails? details, UserPresence? presence, string? headshot, bool isFriend)
        {
            var person = new SearchPerson
            {
                UserId = userId,
                DisplayName = details?.Data.DisplayName ?? userId.ToString(Locale.CurrentCulture),
                Username = details?.Data.Name ?? String.Empty,
                IsVerified = details?.Data.HasVerifiedBadge ?? false,
                HeadshotUrl = headshot ?? details?.Thumbnail?.ImageUrl,
                IsFriend = isFriend
            };

            ApplyPresence(person, presence);
            return person;
        }

        private static void ApplyPresence(SearchPerson person, UserPresence? presence)
        {
            person.Status = presence?.UserPresenceType ?? UserPresenceType.Offline;
            person.GameName = !String.IsNullOrWhiteSpace(presence?.LastLocation) ? presence!.LastLocation : null;
            person.PlaceId = presence?.PlaceId ?? 0;
            person.RootPlaceId = presence?.RootPlaceId ?? 0;
            person.ServerId = String.IsNullOrEmpty(presence?.GameId) ? null : presence!.GameId;
        }

        private async Task JoinAsync(SearchPerson? person)
        {
            if (person is null || !person.CanJoin)
                return;

            try
            {
                App.Logger.Info($"People browser: joining {person.DisplayName} in {person.PlaceId}/{person.ServerId}");
                GameServers.Join(person.PlaceId, person.ServerId!);
            }
            catch (Exception ex)
            {
                App.Logger.Error("People browser: failed to join");
                App.Logger.Error(ex);
            }

            await Task.CompletedTask;
        }

        private void ViewGame(SearchPerson? person)
        {
            if (person is null || !person.CanViewGame)
                return;

            try
            {
                long place = person.RootPlaceId > 0 ? person.RootPlaceId : person.PlaceId;
                GameServers.OpenGamePage(place);
            }
            catch (Exception ex)
            {
                App.Logger.Error("People browser: failed to open game page");
                App.Logger.Error(ex);
            }
        }

        private async Task AddFriendAsync(SearchPerson? person)
        {
            if (person is null || !person.CanAddFriend)
                return;

            person.IsWorking = true;

            try
            {
                PeopleApi.FriendRequestResult result = await PeopleApi.SendFriendRequestAsync(person.UserId);

                switch (result)
                {
                    case PeopleApi.FriendRequestResult.Success:
                        person.IsPending = true;
                        _pendingIds.Add(person.UserId);
                        UpdatePendingTimer();
                        App.Logger.Info($"People browser: friend request sent to {person.DisplayName}");
                        break;

                    case PeopleApi.FriendRequestResult.AlreadyFriend:
                    case PeopleApi.FriendRequestResult.AlreadyRequested:
                        person.IsPending = true;
                        _pendingIds.Add(person.UserId);
                        UpdatePendingTimer();
                        App.Logger.Info($"People browser: friendship with {person.DisplayName} already exists");
                        break;

                    case PeopleApi.FriendRequestResult.Self:
                        App.Logger.Warn("People browser: cannot send a request to yourself");
                        break;

                    case PeopleApi.FriendRequestResult.Blocked:
                        App.Logger.Warn($"People browser: request to {person.DisplayName} blocked");
                        break;

                    default:
                        App.Logger.Warn($"People browser: friend request failed for {person.DisplayName}");
                        break;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("People browser: failed to send friend request");
                App.Logger.Error(ex);
            }
            finally
            {
                person.IsWorking = false;
            }
        }

        private void OnFriendshipChanged(object? sender, FriendshipNotification note)
        {
            if (note.EventArgs is not { } args || note.Type is null)
                return;

            long me = _me != 0 ? _me : App.Cookies.CurrentUser?.Id ?? 0;

            if (me == 0)
                return;

            long other = args.UserId1 == me ? args.UserId2 : args.UserId1;
            bool byMe  = (args.UserId1 == me &&  args.UserId1IsActor)
                      || (args.UserId2 == me && !args.UserId1IsActor);

            if (other == 0 || other == me)
                return;

            string type = note.Type;

            Dispatcher.UIThread.Post(() => ApplyFriendshipChange(other, type, byMe));
        }

        private void ApplyFriendshipChange(long other, string type, bool byMe)
        {
            if (_disposed)
                return;

            switch (type)
            {
                case FriendshipNotification.CreatedType:
                {
                    _pendingIds.Remove(other);
                    _friendIds.Add(other);

                    foreach (SearchPerson p in People.Where(p => p.UserId == other))
                    {
                        p.IsFriend    = true;
                        p.IsPending   = false;
                        p.HasIncoming = false;
                    }

                    foreach (SearchPerson p in _friendsCache.Where(p => p.UserId == other))
                    {
                        p.IsFriend    = true;
                        p.IsPending   = false;
                        p.HasIncoming = false;
                    }

                    UpdatePendingTimer();

                    _friendsLoaded = false;
                    _friendsCache.Clear();

                    if (_showingFriends && String.IsNullOrWhiteSpace(_query))
                        _ = LoadFriendsAsync();

                    App.Logger.Info($"People browser: {(byMe ? "added" : "was added by")} {other}");
                    break;
                }

                case FriendshipNotification.DestroyedType:
                {
                    _pendingIds.Remove(other);
                    _friendIds.Remove(other);

                    foreach (SearchPerson p in People.Where(p => p.UserId == other))
                        p.IsFriend = false;

                    _friendsCache.RemoveAll(p => p.UserId == other);

                    if (_showingFriends && String.IsNullOrWhiteSpace(_query))
                    {
                        People.ReplaceAll(_friendsCache);

                        if (_friendsCache.Count == 0)
                            SelectedIndex = -1;
                        else if (SelectedIndex >= _friendsCache.Count)
                            SelectedIndex = _friendsCache.Count - 1;
                    }

                    UpdatePendingTimer();
                    Refreshed();

                    App.Logger.Info($"People browser: {(byMe ? "removed" : "lost")} friend {other}");
                    break;
                }

                case FriendshipNotification.RequestedType:
                    App.Logger.Info($"People browser: friend request event for {other}");
                    break;

                default:
                    App.Logger.Info($"People browser: unhandled friendship type {type}");
                    break;
            }
        }

        private void Refreshed()
        {
            OnPropertyChanged(nameof(ShowPeople));
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(ShowHeader));
            OnPropertyChanged(nameof(EmptyText));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _disposeCts.Cancel();
            _disposeCts.Dispose();
            _debounce.Stop();
            _pendingTimer.Stop();

            RealtimeMessaging.AnyFriendshipChanged -= OnFriendshipChanged;

            GC.SuppressFinalize(this);
        }
    }
}