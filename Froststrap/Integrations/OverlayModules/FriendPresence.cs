using System.Diagnostics.CodeAnalysis;

using Froststrap.Enums;
using Froststrap.Models.APIs.RealtimeMessaging;
using Froststrap.Models.APIs.Roblox;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Integrations.OverlayModules
{
    internal class FriendPresence(ActivityWatcher? activityWatcher, RealtimeMessaging? messaging = null) : IDisposable
    {
        private const string LOG_IDENT = "FriendPresence";

        private static readonly TimeSpan ResyncInterval = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan OfflineInterval = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan FriendsRefresh = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(15);

        private const int BatchSize = 50;
        private const int MaxPerUpdate = 3;

        public event EventHandler<FriendPresenceChange>? Changed;
        public event EventHandler<IReadOnlyCollection<long>>? Updated;

        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Owned by the caller, not by FriendPresence.")]
        private readonly ActivityWatcher? _activityWatcher = activityWatcher;

        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Owned by the caller, not by FriendPresence.")]
        private readonly RealtimeMessaging? _messaging = messaging;

        private readonly Lock _lock = new();

        private readonly Dictionary<(long UserId, string Place), DateTime> _notified = [];

        private Dictionary<long, UserPresence> _last = [];
        private List<long> _friends = [];
        private HashSet<long> _friendIds = [];
        private DateTime _friendsFetched = DateTime.MinValue;

        private CancellationTokenSource? _cancellation;
        private bool _seeded;

        public IReadOnlyList<long> FriendIds => [.. _friends];

        public IReadOnlyDictionary<long, UserPresence> Presences
        {
            get
            {
                lock (_lock)
                    return new Dictionary<long, UserPresence>(_last);
            }
        }

        public bool HasLooked => _seeded;

        public UserPresence? GetPresence(long userId)
        {
            lock (_lock)
                return _last.GetValueOrDefault(userId);
        }

        public void Start()
        {
            if (_cancellation is not null)
                return;

            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;

            _messaging?.PresenceBulk += OnPresenceBulk;
            _messaging?.FriendshipChanged += OnFriendshipChanged;

            _ = Task.Run(() => RunAsync(token));
        }

        public void Stop()
        {
            _messaging?.PresenceBulk -= OnPresenceBulk;
            _messaging?.FriendshipChanged -= OnFriendshipChanged;

            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _seeded = false;
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                await PollAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"{LOG_IDENT}::RunAsync", "Failed to seed presence");
                App.Logger.Error(ex);
            }

            try
            {
                while (!token.IsCancellationRequested)
                {
                    TimeSpan delay = _messaging?.IsConnected == true ? ResyncInterval : OfflineInterval;

                    await Task.Delay(delay, token);

                    try
                    {
                        await PollAsync();
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"{LOG_IDENT}::RunAsync", "Failed to reconcile presence");
                        App.Logger.Error(ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public async Task PollAsync()
        {
            if (!await App.Cookies.EnsureLoadedAsync() || App.Cookies.CurrentUser is not AuthenticatedUser me)
                return;

            if (DateTime.UtcNow - _friendsFetched > FriendsRefresh)
            {
                var friends = await Http.GetJson<ApiArrayResponse<FriendResponse>>(
                    UrlBuilder.BuildApiUrl("friends", $"v1/users/{me.Id}/friends"));

                _friends = [.. friends?.Data?.Select(x => x.Id).Where(x => x > 0).Distinct() ?? []];
                _friendIds = [.. _friends];
                _friendsFetched = DateTime.UtcNow;

                App.Logger.Info($"{LOG_IDENT}::PollAsync", $"Keeping an eye on {_friends.Count} friends");
            }

            if (_friends.Count == 0)
            {
                _seeded = true;
                Updated?.Invoke(this, []);
                return;
            }

            var now = new Dictionary<long, UserPresence>();

            foreach (long[] batch in _friends.Chunk(BatchSize))
            {
                var response = await AccountRequests.PostJsonAsync<UserPresenceResponse>(
                    UrlBuilder.BuildApiUrl("presence", "v1/presence/users"), new { userIds = batch });

                foreach (UserPresence presence in response?.UserPresences ?? [])
                    now[presence.UserId] = presence;
            }

            List<FriendPresenceChange> changes;
            List<long> changed;

            lock (_lock)
            {
                changes = _seeded ? Compare(_last, now) : [];
                changed = Diff(_last, now);

                _last = now;
                _seeded = true;
            }

            Publish(changes, changed);
        }

        private void OnPresenceBulk(object? sender, IReadOnlyList<PresenceNotification> notifications)
        {
            List<FriendPresenceChange> changes;
            List<long> changed;

            lock (_lock)
            {
                if (!_seeded)
                    return;

                var updates = new Dictionary<long, UserPresence>();

                foreach (PresenceNotification note in notifications)
                {
                    if (note.PresenceReport is null)
                        continue;

                    if (!String.Equals(note.Type, PresenceNotification.PresenceChangedType, StringComparison.OrdinalIgnoreCase))
                    {
                        App.Logger.Info($"{LOG_IDENT}::OnPresenceBulk: unhandled presence type {note.Type}");
                        continue;
                    }

                    long userId = note.PresenceReport.UserId != 0 ? note.PresenceReport.UserId : note.UserId;

                    if (userId == 0)
                        continue;

                    if (_friendIds.Count > 0 && !_friendIds.Contains(userId))
                        continue;

                    if (_last.TryGetValue(userId, out UserPresence? existing) && SamePresence(existing, note.PresenceReport))
                        continue;

                    updates[userId] = note.PresenceReport;
                }

                if (updates.Count == 0)
                    return;

                changes = Compare(_last, updates);
                changed = [.. updates.Keys];

                foreach ((long userId, UserPresence presence) in updates)
                    _last[userId] = presence;
            }

            Publish(changes, changed);
        }

        private void OnFriendshipChanged(object? sender, FriendshipNotification note)
        {
            if (note.EventArgs is not { } args || note.Type is null)
                return;

            if (App.Cookies.CurrentUser is not AuthenticatedUser me)
                return;

            long other = args.UserId1 == me.Id ? args.UserId2 : args.UserId1;
            bool byMe  = (args.UserId1 == me.Id &&  args.UserId1IsActor)
                      || (args.UserId2 == me.Id && !args.UserId1IsActor);

            if (other == 0 || other == me.Id)
                return;

            bool friendsChanged = false;

            lock (_lock)
            {
                switch (note.Type)
                {
                    case FriendshipNotification.CreatedType:
                        if (!_friendIds.Contains(other))
                        {
                            _friends.Add(other);
                            _friendIds.Add(other);
                            friendsChanged = true;
                        }

                        _friendsFetched = DateTime.MinValue;
                        break;

                    case FriendshipNotification.DestroyedType:
                        if (_friendIds.Remove(other))
                        {
                            _friends.Remove(other);
                            friendsChanged = true;
                        }

                        _last.Remove(other);

                        var stale = _notified.Keys.Where(k => k.UserId == other).ToList();

                        foreach (var key in stale)
                            _notified.Remove(key);

                        _friendsFetched = DateTime.MinValue;
                        break;

                    case FriendshipNotification.RequestedType:
                        App.Logger.Info($"{LOG_IDENT}::OnFriendshipChanged",
                            byMe ? $"Friend request sent to {other}"
                                 : $"Friend request received from {other}");
                        return;

                    default:
                        App.Logger.Info($"{LOG_IDENT}::OnFriendshipChanged",
                            $"Unhandled friendship type {note.Type}");
                        return;
                }
            }

            if (!friendsChanged)
                return;

            App.Logger.Info($"{LOG_IDENT}::OnFriendshipChanged",
                note.Type == FriendshipNotification.CreatedType
                    ? (byMe ? $"Added {other}" : $"{other} added you back")
                    : (byMe ? $"Unfriended {other}" : $"{other} unfriended you"));

            Updated?.Invoke(this, [other]);

            _ = PollAsync();
        }

        private static List<long> Diff(Dictionary<long, UserPresence> before, Dictionary<long, UserPresence> now)
        {
            var changed = new List<long>();

            foreach ((long userId, UserPresence presence) in now)
            {
                if (!before.TryGetValue(userId, out UserPresence? was) || !SamePresence(was, presence))
                    changed.Add(userId);
            }

            foreach (long userId in before.Keys)
            {
                if (!now.ContainsKey(userId))
                    changed.Add(userId);
            }

            return changed;
        }

        private void Publish(List<FriendPresenceChange> changes, List<long> changed)
        {
            if (changed.Count > 0)
                Updated?.Invoke(this, changed);

            foreach (FriendPresenceChange change in changes.OrderBy(x => x.Kind).Take(MaxPerUpdate))
                Changed?.Invoke(this, change);
        }

        private static bool SamePresence(UserPresence a, UserPresence b) =>
            a.UserPresenceType == b.UserPresenceType &&
            a.PlaceId == b.PlaceId &&
            a.RootPlaceId == b.RootPlaceId &&
            a.UniverseId == b.UniverseId &&
            String.Equals(a.GameId, b.GameId, StringComparison.Ordinal) &&
            String.Equals(a.LastLocation, b.LastLocation, StringComparison.Ordinal);

        private static bool InGame(UserPresence p) => p.UserPresenceType == UserPresenceType.InGame;

        private List<FriendPresenceChange> Compare(
            Dictionary<long, UserPresence> before,
            Dictionary<long, UserPresence> now)
        {
            var changes = new List<FriendPresenceChange>();

            bool inGame = _activityWatcher?.InGame == true;
            string myServer = inGame ? _activityWatcher!.Data.JobId : String.Empty;
            long myUniverse = inGame ? _activityWatcher!.Data.UniverseId : 0;

            foreach ((long userId, UserPresence presence) in now)
            {
                if (!InGame(presence) || String.IsNullOrWhiteSpace(presence.LastLocation))
                    continue;

                before.TryGetValue(userId, out UserPresence? was);

                if (!String.IsNullOrEmpty(myServer)
                    && String.Equals(presence.GameId, myServer, StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(was?.GameId, myServer, StringComparison.OrdinalIgnoreCase))
                {
                    Add(userId, FriendPresenceKind.JoinedYourServer, presence, myServer);
                    continue;
                }

                if (was is not null && InGame(was) && was.UniverseId == presence.UniverseId)
                    continue;

                FriendPresenceKind kind = myUniverse != 0 && presence.UniverseId == myUniverse
                    ? FriendPresenceKind.PlayingYourGame
                    : FriendPresenceKind.StartedPlaying;

                Add(userId, kind, presence, (presence.UniverseId ?? 0).ToString(CultureInfo.InvariantCulture));
            }

            return changes;

            void Add(long userId, FriendPresenceKind kind, UserPresence presence, string place)
            {
                if (_notified.TryGetValue((userId, place), out DateTime at) && DateTime.UtcNow - at < Cooldown)
                    return;

                _notified[(userId, place)] = DateTime.UtcNow;

                changes.Add(new FriendPresenceChange(userId, kind, presence.LastLocation!,
                    presence.PlaceId ?? 0, presence.RootPlaceId ?? 0,
                    String.IsNullOrEmpty(presence.GameId) ? null : presence.GameId));
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}