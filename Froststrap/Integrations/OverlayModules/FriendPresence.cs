using Froststrap.Enums.Overlay;
using Froststrap.Models.APIs.Roblox;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Integrations.OverlayModules
{
    internal class FriendPresence : IDisposable
    {
        private const string LOG_IDENT = "FriendPresence";

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(45);
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

        private readonly Dictionary<(long UserId, string Place), DateTime> _notified = new();

        private Dictionary<long, UserPresence> _last = new();
        private List<long> _friends = new();
        private DateTime _friendsFetched = DateTime.MinValue;

        private CancellationTokenSource? _cancellation;
        private bool _seeded;

        public IReadOnlyList<long> FriendIds => _friends.ToList();
        public IReadOnlyDictionary<long, UserPresence> Presences => new Dictionary<long, UserPresence>(_last);
        public bool HasLooked => _seeded;

        public FriendPresence(ActivityWatcher? activityWatcher)
        {
            _activityWatcher = activityWatcher;
        }

        public void Start()
        {
            if (_cancellation is not null)
                return;

            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;
            _ = Task.Run(() => RunAsync(token));
        }

        public void Stop()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _seeded = false;
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await PollAsync();
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"{LOG_IDENT}::RunAsync", "Failed to check on friends");
                        App.Logger.Error(ex);
                    }

                    await Task.Delay(PollInterval, token);
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

            List<FriendPresenceChange> changes = _seeded ? Compare(now) : new List<FriendPresenceChange>();

            _last = now;
            _seeded = true;

            Updated?.Invoke(this, EventArgs.Empty);

            foreach (FriendPresenceChange change in changes.OrderBy(x => x.Kind).Take(MaxPerPoll))
                Changed?.Invoke(this, change);
        }

        private static bool InGame(UserPresence p) => p.UserPresenceType == PresenceInGame;

        private List<FriendPresenceChange> Compare(Dictionary<long, UserPresence> now)
        {
            var changes = new List<FriendPresenceChange>();

            bool inGame = _activityWatcher?.InGame == true;
            string myServer = inGame ? _activityWatcher!.Data.JobId : String.Empty;
            long myUniverse = inGame ? _activityWatcher!.Data.UniverseId : 0;

            foreach ((long userId, UserPresence presence) in now)
            {
                if (!InGame(presence) || String.IsNullOrWhiteSpace(presence.LastLocation))
                    continue;

                _last.TryGetValue(userId, out UserPresence? before);

                if (!String.IsNullOrEmpty(myServer)
                    && String.Equals(presence.GameId, myServer, StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(before?.GameId, myServer, StringComparison.OrdinalIgnoreCase))
                {
                    Add(userId, FriendPresenceKind.JoinedYourServer, presence, myServer);
                    continue;
                }

                if (before is not null && InGame(before) && before.UniverseId == presence.UniverseId)
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