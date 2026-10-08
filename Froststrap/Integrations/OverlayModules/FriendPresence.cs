using Froststrap.Enums.Overlay;
using Froststrap.Models.APIs.Roblox;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Integrations.OverlayModules
{
    internal class FriendPresence : IDisposable
    {
        private const string LOG_IDENT = "FriendPresence";

        private static readonly TimeSpan ReconcileInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FriendsRefresh = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(15);

        private const int BatchSize = 50;
        private const int MaxPerPoll = 3;

#pragma warning disable CA1823
        private const int PresenceOffline = 0;
        private const int PresenceOnline = 1;
        private const int PresenceInGame = 2;
        private const int PresenceInStudio = 3;
#pragma warning restore CA1823

        public event EventHandler<FriendPresenceChange>? Changed;
        public event EventHandler? Updated;

        private readonly ActivityWatcher? _activityWatcher;
        private readonly RealtimeMessaging? _messaging;

        private readonly Dictionary<(long UserId, string Place), DateTime> _notified = new();

        private Dictionary<long, UserPresence> _last = new();
        private List<long> _friends = new();
        private DateTime _friendsFetched = DateTime.MinValue;

        private CancellationTokenSource? _cancellation;
        private bool _seeded;

        public IReadOnlyList<long> FriendIds => _friends.ToList();
        public IReadOnlyDictionary<long, UserPresence> Presences => new Dictionary<long, UserPresence>(_last);
        public bool HasLooked => _seeded;

        public FriendPresence(ActivityWatcher? activityWatcher, RealtimeMessaging? messaging = null)
        {
            _activityWatcher = activityWatcher;
            _messaging = messaging;
        }

        public void Start()
        {
            if (_cancellation is not null)
                return;

            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;

            if (_messaging is not null)
                _messaging.PresenceBulk += OnPresenceBulk;

            _ = Task.Run(() => RunAsync(token));
        }

        public void Stop()
        {
            if (_messaging is not null)
                _messaging.PresenceBulk -= OnPresenceBulk;

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
                    await Task.Delay(ReconcileInterval, token);

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
                // expected on shutdown
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

                _friends = friends?.Data?.Select(x => x.Id).Where(x => x > 0).Distinct().ToList() ?? new List<long>();
                _friendsFetched = DateTime.UtcNow;

                App.Logger.Info($"{LOG_IDENT}::PollAsync", $"Keeping an eye on {_friends.Count} friends");
            }

            if (_friends.Count == 0)
            {
                _seeded = true;
                Updated?.Invoke(this, EventArgs.Empty);
                return;
            }

            var now = new Dictionary<long, UserPresence>();

            foreach (long[] batch in _friends.Chunk(BatchSize))
            {
                var response = await AccountRequests.PostJsonAsync<UserPresenceResponse>(
                    UrlBuilder.BuildApiUrl("presence", "v1/presence/users"), new { userIds = batch });

                foreach (UserPresence presence in response?.UserPresences ?? new List<UserPresence>())
                    now[presence.UserId] = presence;
            }

            List<FriendPresenceChange> changes = _seeded ? Compare(_last, now) : new List<FriendPresenceChange>();

            _last = now;
            _seeded = true;

            Updated?.Invoke(this, EventArgs.Empty);

            foreach (FriendPresenceChange change in changes.OrderBy(x => x.Kind).Take(MaxPerPoll))
                Changed?.Invoke(this, change);
        }

        private void OnPresenceBulk(object? sender, IReadOnlyList<PresenceNotification> notifications)
        {
            if (!_seeded)
                return;

            var updates = new Dictionary<long, UserPresence>();

            foreach (PresenceNotification note in notifications)
            {
                if (note.PresenceReport is null)
                    continue;

                if (!String.Equals(note.Type, "PresenceChanged", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.Info($"{LOG_IDENT}::OnPresenceBulk: unhandled presence type {note.Type}");
                    continue;
                }

                long userId = note.PresenceReport.UserId != 0 ? note.PresenceReport.UserId : note.UserId;

                if (userId == 0)
                    continue;

                if (_friends.Count > 0 && !_friends.Contains(userId))
                    continue;

                if (_last.TryGetValue(userId, out UserPresence? existing) && SamePresence(existing, note.PresenceReport))
                    continue;

                updates[userId] = note.PresenceReport;
            }

            if (updates.Count == 0)
                return;

            Dictionary<long, UserPresence> snapshot = _last;

            List<FriendPresenceChange> changes = Compare(snapshot, updates);

            foreach ((long userId, UserPresence presence) in updates)
                _last[userId] = presence;

            Updated?.Invoke(this, EventArgs.Empty);

            foreach (FriendPresenceChange change in changes.OrderBy(x => x.Kind).Take(MaxPerPoll))
                Changed?.Invoke(this, change);
        }

        private static bool SamePresence(UserPresence a, UserPresence b) =>
            a.UserPresenceType == b.UserPresenceType &&
            a.PlaceId == b.PlaceId &&
            a.RootPlaceId == b.RootPlaceId &&
            a.UniverseId == b.UniverseId &&
            String.Equals(a.GameId, b.GameId, StringComparison.Ordinal) &&
            String.Equals(a.LastLocation, b.LastLocation, StringComparison.Ordinal);

        private static bool InGame(UserPresence p) => p.UserPresenceType == PresenceInGame;

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