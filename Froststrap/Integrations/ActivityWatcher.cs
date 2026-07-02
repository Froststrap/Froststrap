using System.Runtime.InteropServices;

namespace Froststrap.Integrations
{
    public class ActivityWatcher : IDisposable
    {
        private const string GameMessageEntry = "[FLog::Output] [BloxstrapRPC]";
        private const string GameJoiningEntry = "[FLog::Output] ! Joining game";

        // these entries are technically volatile!
        // they only get printed depending on their configured FLog level, which could change at any time
        // while levels being changed is fairly rare, please limit the number of varying number of FLog types you have to use, if possible

        private const string GameTeleportingEntry = "[FLog::UgcExperienceController] UgcExperienceController: doTeleport: joinScriptUrl";
        private const string GameJoiningUniverseEntry = "[FLog::GameJoinLoadTime] Report game_join_loadtime:";
        private const string GameJoiningUDMUXEntry = "[FLog::Network] UDMUX Address = ";
        private const string GameJoinedEntry = "[FLog::Network] serverId:";
        private const string GameDisconnectedEntry = "[FLog::Network] Time to disconnect replication data:";
        private const string GameLeavingEntry = "[FLog::SingleSurfaceApp] leaveUGCGameInternal";
        private const string GameLeavingEntrySober = "app_interface$json: {\"type\":\"game_left\"}";
        private const string AppCloseEntrySober = "app: lifecycle: will_do_clean_exit";
        private const string GameDisconnectReasonEntry = "[FLog::Network] Sending disconnect with reason:";
        private const string GameServerUptimeEntry = "[FLog::Output] Server Prefix: ";

        private const string StudioPlaceOpenEntry = "[FLog::PlaceManager] Start to open place";
        private const string StudioPlaceCloseEntry = "[FLog::PlaceManager] PlaceManager::closeCurrentPlayDoc";

        private const string GameJoiningEntryPattern = @"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)";
        private const string GameJoiningUniversePattern = @"universeid:([0-9]+)";
        private const string GameJoiningUniverseUserIDPattern = @"userid:([0-9]+)";
        private const string GameJoinReferralPattern = @"referral_page:([^,]+)";
        private const string GameTeleportJoinTypePattern = @"JoinTypeId""%3a(\d+)%2c";
        private const string GameJoiningUDMUXPattern = @"UDMUX Address = ([0-9\.]+), Port = [0-9]+ \| RCC Server Address = ([0-9\.]+), Port = [0-9]+";
        private const string GameJoinedEntryPattern = @"serverId: ([0-9\.]+)\|[0-9]+";
        private const string GameMessageEntryPattern = @"\[BloxstrapRPC\] (.*)";
        private const string GameDisconnectReasonPattern = @"Sending disconnect with reason: (\d+)";
        private const string GameServerUptimePattern = @"Server Prefix:.+_(\d{8}T\d{6}Z)_RCC_[0-9a-z]+";

        private int _logEntriesRead = 0;
        private bool _teleportMarker = false;
        private bool _reservedTeleportMarker = false;
        private bool _shouldAutoRejoin = false;

        private static readonly string GameHistoryCachePath = Path.Combine(Paths.Cache, "GameHistory.json");

        private static readonly JsonSerializerOptions _loadOptions = new() { PropertyNamingPolicy = null };
        private static readonly JsonSerializerOptions _saveOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        public event EventHandler? OnHistoryUpdated;

        public event EventHandler<string>? OnLogEntry;
        public event EventHandler? ShowNotif;
        public event EventHandler? OnGameJoin;
        public event EventHandler? OnGameLeave;
        public event EventHandler? OnStudioPlaceOpened;
        public event EventHandler? OnStudioPlaceClosed;
        public event EventHandler? OnLogOpen;
        public event EventHandler? OnAppClose;
        public event EventHandler<Message>? OnRPCMessage;
        public event EventHandler<StudioMessage>? OnStudioRPCMessage;

        private DateTime LastRPCRequest;

        private readonly LaunchMode _launchMode;
        private readonly int _robloxPID;

        public string LogLocation = null!;

        public bool InGame = false;
        public bool InStudioPlace = false;
        public bool InRobloxStudio = false;

        private const int HttpPort = 4875;
        private HttpListener? _httpListener;
        private readonly CancellationTokenSource _httpCancellationTokenSource = new();

        public ActivityData Data { get; private set; } = new();

        /// <summary>
        /// Ordered by newest to oldest
        /// </summary>
        public List<ActivityData> History = [];

        public bool IsDisposed = false;

        public static void CloseProcess(int pid)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                process.Kill();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public ActivityWatcher(string? logFile = null, LaunchMode launchMode = LaunchMode.Player, int RobloxPID = 0)
        {
            if (!String.IsNullOrEmpty(logFile))
                LogLocation = logFile;

            _launchMode = launchMode;
            _robloxPID = RobloxPID;

            if (_launchMode == LaunchMode.Studio || _launchMode == LaunchMode.StudioAuth)
            {
                InRobloxStudio = true;
                StartHTTPServer();
            }

            LoadGameHistory();
        }

        public async void Start()
        {
            const string LOG_IDENT = "ActivityWatcher::Start";

            // okay, here's the process:
            //
            // - tail the latest log file from %localappdata%\roblox\logs
            // - check for specific lines to determine player's game activity as shown below:
            //
            // - get the place id, job id and machine address from '! Joining game '{{JOBID}}' place {{PLACEID}} at {{MACHINEADDRESS}}' entry
            // - confirm place join with 'serverId: {{MACHINEADDRESS}}|{{MACHINEPORT}}' entry
            // - check for leaves/disconnects with 'Time to disconnect replication data: {{TIME}}' entry
            //
            // we'll tail the log file continuously, monitoring for any log entries that we need to determine the current game activity

            FileInfo logFileInfo;

            if (String.IsNullOrEmpty(LogLocation))
            {
                string logDirectory = Paths.RobloxLogs;

                if (!Directory.Exists(logDirectory))
                    return;

                // we need to make sure we're fetching the absolute latest log file
                // if roblox doesn't start quickly enough, we can wind up fetching the previous log file
                // good rule of thumb is to find a log file that was created in the last 15 seconds or so

                App.Logger.WriteLine(LOG_IDENT, "Opening Roblox log file...");

                string logNameFilter = (InRobloxStudio || _launchMode == LaunchMode.Studio || _launchMode == LaunchMode.StudioAuth)
                    ? "Studio"
                    : "Player";

                while (true)
                {
                    var candidates = new DirectoryInfo(logDirectory)
                        .GetFiles()
                        .Where(x => x.Name.Contains(logNameFilter, StringComparison.OrdinalIgnoreCase) && x.CreationTime <= DateTime.Now)
                        .OrderByDescending(x => x.CreationTime)
                        .ToList();

                    if (candidates.Count == 0)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"No '{logNameFilter}' log files found, waiting...");
                        await Task.Delay(1000);
                        continue;
                    }

                    logFileInfo = candidates.First();

                    if (logFileInfo.CreationTime.AddSeconds(15) > DateTime.Now)
                        break;

                    App.Logger.WriteLine(LOG_IDENT, $"Could not find recent enough log file, waiting... (newest is {logFileInfo.Name})");
                    await Task.Delay(1000);
                }

                LogLocation = logFileInfo.FullName;
            }
            else
            {
                logFileInfo = new FileInfo(LogLocation);
            }

            OnLogOpen?.Invoke(this, EventArgs.Empty);

            var logFileStream = logFileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            App.Logger.WriteLine(LOG_IDENT, $"Opened {LogLocation}");

            using var streamReader = new StreamReader(logFileStream);

            while (!IsDisposed)
            {
                string? log = await streamReader.ReadLineAsync();

                if (log is null)
                    await Task.Delay(1000);
                else
                    ReadLogEntry(log);
            }
        }

        private void ReadLogEntry(string entry)
        {
            const string LOG_IDENT = "ActivityWatcher::ReadLogEntry";

            OnLogEntry?.Invoke(this, entry);

            _logEntriesRead += 1;

            // debug stats to ensure that the log reader is working correctly
            // if more than 1000 log entries have been read, only log per 100 to save on spam
            if (_logEntriesRead <= 1000 && _logEntriesRead % 50 == 0)
                App.Logger.WriteLine(LOG_IDENT, $"Read {_logEntriesRead} log entries");
            else if (_logEntriesRead % 100 == 0)
                App.Logger.WriteLine(LOG_IDENT, $"Read {_logEntriesRead} log entries");

            string? logMessage = ExtractLogMessage(entry);
            if (string.IsNullOrEmpty(logMessage))
                return;

            if (InRobloxStudio || _launchMode == LaunchMode.Studio || _launchMode == LaunchMode.StudioAuth)
            {
                ProcessStudioLogEntry(logMessage);
            }
            else
            {
                ProcessPlayerLogEntry(logMessage);
            }
        }

        private static string? ExtractLogMessage(string entry)
        {
            // Sober prefixes lines like:
            // "info: Roblox: ... [FLog::Output] ..."
            // so prefer trimming to the first structured Roblox log token.
            int fLogIndex = entry.IndexOf("[FLog::", StringComparison.Ordinal);
            int dfLogIndex = entry.IndexOf("[DFLog::", StringComparison.Ordinal);

            int tokenIndex = -1;
            if (fLogIndex >= 0 && dfLogIndex >= 0)
                tokenIndex = Math.Min(fLogIndex, dfLogIndex);
            else if (fLogIndex >= 0)
                tokenIndex = fLogIndex;
            else if (dfLogIndex >= 0)
                tokenIndex = dfLogIndex;

            if (tokenIndex >= 0)
                return entry[tokenIndex..];

            int logMessageIdx = entry.IndexOf(' ');
            if (logMessageIdx == -1)
                return null;

            return entry[(logMessageIdx + 1)..];
        }

        private void ProcessStudioLogEntry(string logMessage)
        {
            const string LOG_IDENT = "ActivityWatcher::ProcessStudioLogEntry";

            // incase this got called and InRobloxStudio is still false
            if (!InRobloxStudio)
            {
                InRobloxStudio = true;
            }

            // i need to find more logs stuff for studio lowkey
            if (!InStudioPlace)
            {
                if (logMessage.StartsWith(StudioPlaceOpenEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Studio place opened");
                    InStudioPlace = true;

                    OnStudioPlaceOpened?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (InStudioPlace)
            {
                if (logMessage.StartsWith(StudioPlaceCloseEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Studio place closed");
                    InStudioPlace = false;

                    OnStudioPlaceClosed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private async void ProcessPlayerLogEntry(string logMessage)
        {
            const string LOG_IDENT = "ActivityWatcher::ProcessPlayerLogEntry";

            if (logMessage.StartsWith(GameLeavingEntry) || logMessage.StartsWith(GameLeavingEntrySober) || logMessage.StartsWith(AppCloseEntrySober))
            {
                App.Logger.WriteLine(LOG_IDENT, "User is back into the desktop app");

                OnAppClose?.Invoke(this, EventArgs.Empty);

                if (Data.PlaceId != 0 && !InGame)
                {
                    App.Logger.WriteLine(LOG_IDENT, "User appears to be leaving from a cancelled/errored join");
                    Data = new();
                }

                return;
            }

            if (logMessage.StartsWith(GameDisconnectReasonEntry))
            {
                var match = Regex.Match(logMessage, GameDisconnectReasonPattern);
                if (match.Success && match.Groups.Count == 2)
                {
                    int reasonCode = int.Parse(match.Groups[1].Value);

                    if (reasonCode == 1)
                    {
                        _shouldAutoRejoin = true;
                        App.Logger.WriteLine(LOG_IDENT, $"Inactivity timeout detected (reason code: {reasonCode})");
                    }
                    if (reasonCode == 277)
                    {
                        _shouldAutoRejoin = true;
                        App.Logger.WriteLine(LOG_IDENT, $"Internet Disconnection detected (reason code: {reasonCode})");
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Disconnect reason code: {reasonCode}");
                    }
                }
            }

            if (!InGame && Data.PlaceId == 0)
            {
                // We are not in a game, nor are in the process of joining one

                if (logMessage.StartsWith(GameJoiningEntry))
                {
                    Match match = Regex.Match(logMessage, GameJoiningEntryPattern);

                    if (match.Groups.Count != 4)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to assert format for game join entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    InGame = false;
                    Data.PlaceId = long.Parse(match.Groups[2].Value);
                    Data.JobId = match.Groups[1].Value;
                    Data.MachineAddress = match.Groups[3].Value;

                    if (_teleportMarker)
                    {
                        Data.IsTeleport = true;
                        _teleportMarker = false;
                    }

                    if (_reservedTeleportMarker)
                    {
                        Data.ServerType = ServerType.Reserved;
                        _reservedTeleportMarker = false;
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Joining Game ({Data})");
                }
            }
            else if (!InGame && Data.PlaceId != 0)
            {
                // We are not confirmed to be in a game, but we are in the process of joining one

                if (logMessage.Contains(GameJoiningUniverseEntry))
                {
                    // on linux the log goes referalpage, userid then universe id, on windows its diffrent, thats why we split all these
                    var universeMatch = Regex.Match(logMessage, GameJoiningUniversePattern, RegexOptions.IgnoreCase);
                    if (universeMatch.Success)
                    {
                        Data.UniverseId = Int64.Parse(universeMatch.Groups[1].Value);
                    }

                    var userMatch = Regex.Match(logMessage, GameJoiningUniverseUserIDPattern, RegexOptions.IgnoreCase);
                    if (userMatch.Success)
                    {
                        Data.UserId = Int64.Parse(userMatch.Groups[1].Value);
                    }

                    if (Data.UniverseId == 0)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to extract UniverseId from game join entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    var referralMatch = Regex.Match(logMessage, GameJoinReferralPattern, RegexOptions.IgnoreCase);
                    if (referralMatch.Groups.Count == 2)
                    {
                        string referral = referralMatch.Groups[1].Value;
                        if (referral.Contains("RequestPrivateGame", StringComparison.OrdinalIgnoreCase) ||
                            referral.Contains("GameDetailPageJSHybridEvent", StringComparison.OrdinalIgnoreCase))
                        {
                            Data.ServerType = ServerType.Private;
                        }
                    }

                    if (History.Count > 0)
                    {
                        var lastActivity = History.First();
                        if (Data.UniverseId == lastActivity.UniverseId && Data.IsTeleport)
                        {
                            Data.RootActivity = lastActivity.RootActivity ?? lastActivity;
                        }
                    }
                }
                else if (logMessage.StartsWith(GameJoiningUDMUXEntry))
                {
                    var match = Regex.Match(logMessage, GameJoiningUDMUXPattern);

                    if (match.Groups.Count != 3 || match.Groups[2].Value != Data.MachineAddress)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to assert format for game join UDMUX entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    Data.MachineAddress = match.Groups[1].Value;

                    App.Logger.WriteLine(LOG_IDENT, $"Server is UDMUX protected ({Data})");
                }
                else if (logMessage.StartsWith(GameJoinedEntry))
                {
                    Match match = Regex.Match(logMessage, GameJoinedEntryPattern);

                    if (match.Groups.Count != 2 || match.Groups[1].Value != Data.MachineAddress)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to assert format for game joined entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Joined Game ({Data})");

                    InGame = true;
                    Data.TimeJoined = DateTime.Now;

                    OnGameJoin?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (InGame && Data.PlaceId != 0)
            {
                // We are confirmed to be in a game

                if (logMessage.StartsWith(GameDisconnectedEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Disconnected from Game ({Data})");

                    InGame = false;
                    Data.TimeLeft = DateTime.Now;
                    AddToHistory(Data);
                    OnGameLeave?.Invoke(this, EventArgs.Empty);

                    var autoRejoinData = Data;
                    Data = new();

                    if (App.Settings.Prop.AutoRejoin)
                    {
                        await Task.Delay(3000);

                        if (_shouldAutoRejoin)
                        {
                            autoRejoinData.RejoinServer(false);

                            // we use this because can you imagine having 5 accs open and we close all of them cuz 1 dced ?
                            CloseProcess(_robloxPID);
                        }
                        else
                        {
                            App.Logger.WriteLine(LOG_IDENT, "No inactivity detected within 3 seconds, skipping auto-rejoin");
                        }
                    }

                    _shouldAutoRejoin = false;
                }
                else if (logMessage.StartsWith(GameTeleportingEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Initiating teleport to server ({Data})");
                    _teleportMarker = true;

                    var joinTypeMatch = Regex.Match(logMessage, GameTeleportJoinTypePattern);
                    if (joinTypeMatch.Success && int.TryParse(joinTypeMatch.Groups[1].Value, out int joinTypeId))
                    {
                        var joinType = (ServerSessionJoinType)joinTypeId;
                        App.Logger.WriteLine(LOG_IDENT, $"Teleport JoinTypeId: {joinTypeId}");

                        if (joinType is ServerSessionJoinType.NewGamePrivateGame or ServerSessionJoinType.SpecificPrivateGame)
                        {
                            _reservedTeleportMarker = true;
                            App.Logger.WriteLine(LOG_IDENT, "Detected reserved server teleport");
                        }
                    }
                    else
                        App.Logger.WriteLine(LOG_IDENT, "Failed to detect teleport type");
                }
                else if (logMessage.StartsWith(GameMessageEntry))
                {
                    var match = Regex.Match(logMessage, GameMessageEntryPattern);

                    if (match.Groups.Count != 2)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to assert format for RPC message entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    string messagePlain = match.Groups[1].Value;
                    Message? message;

                    App.Logger.WriteLine(LOG_IDENT, $"Received message: '{messagePlain}'");

                    if ((DateTime.Now - LastRPCRequest).TotalSeconds <= 1)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Dropping message as ratelimit has been hit");
                        return;
                    }

                    try
                    {
                        message = JsonSerializer.Deserialize<Message>(messagePlain);
                    }
                    catch (Exception)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                        return;
                    }

                    if (message is null)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                        return;
                    }

                    if (string.IsNullOrEmpty(message.Command))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (Command is empty)");
                        return;
                    }

                    if (message.Command == "SetLaunchData")
                    {
                        string? data;

                        try
                        {
                            data = message.Data.Deserialize<string>();
                        }
                        catch (Exception)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                            return;
                        }

                        if (data is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (data.Length > 200)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Data cannot be longer than 200 characters");
                            return;
                        }

                        Data.RPCLaunchData = data;
                    }

                    OnRPCMessage?.Invoke(this, message);

                    LastRPCRequest = DateTime.Now;
                }
                else if (logMessage.StartsWith(GameServerUptimeEntry))
                {
                    Match match = Regex.Match(logMessage, GameServerUptimePattern);

                    if (!match.Success && match.Groups.Count == 2)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to assert format for server uptime entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    string startTime = match.Groups[1].Value;

                    App.Logger.WriteLine(LOG_IDENT, $"Server started at {startTime}");

                    Data.StartTime = DateTime.ParseExact(startTime, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

                    if (App.Settings.Prop.ShowServerDetails && Data.MachineAddressValid)
                        _ = Data.QueryServerLocation();

                    ShowNotif?.Invoke(this, null!);
                }
            }
        }

        private void StartHTTPServer()
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{HttpPort}/");
                _httpListener.Start();

                _ = ListenForHTTPRequests(_httpCancellationTokenSource.Token);

                App.Logger.WriteLine("ActivityWatcher", $"Studio RPC server active on port {HttpPort}");
            }
            catch (Exception ex) { App.Logger.WriteException("ActivityWatcher::Start", ex); }
        }

        public void StopHTTPServer()
        {
            _httpCancellationTokenSource.Cancel();

            if (_httpListener != null)
            {
                try { _httpListener.Close(); }
                catch { }
                _httpListener = null;
            }
        }

        private async Task ListenForHTTPRequests(CancellationToken token)
        {
            while (_httpListener?.IsListening == true && !token.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync().WaitAsync(token);

                    _ = Task.Run(() => ProcessHTTPRequest(context), token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    App.Logger.WriteException("ActivityWatcher::HTTPListener", ex);
                    await Task.Delay(1000, token);
                }
            }
        }

        private void ProcessHTTPRequest(HttpListenerContext context)
        {
            using var response = context.Response;

            try
            {
                if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/rpc")
                {
                    response.StatusCode = 404;
                    return;
                }

                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                string json = reader.ReadToEnd();
                var message = JsonSerializer.Deserialize<StudioMessage>(json);

                if (message != null)
                {
                    if (message.StudioCommand == "SetRichPresence")
                    {
                        var richPresenceData = message.Data.Deserialize<StudioRichPresence>();
                        if (richPresenceData != null)
                            message.Data = JsonSerializer.SerializeToElement(richPresenceData);
                    }

                    OnStudioRPCMessage?.Invoke(this, message);
                    response.StatusCode = 200;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ActivityWatcher::ProcessHTTP", $"Error: {ex.Message}");
                response.StatusCode = 500;
            }
        }

        public void LoadGameHistory()
        {
            try
            {
                if (!File.Exists(GameHistoryCachePath))
                {
                    App.Logger.WriteLine("ActivityWatcher::LoadGameHistory", "No existing game history cache found");
                    History = [];
                    return;
                }

                string json = File.ReadAllText(GameHistoryCachePath);
                var gameHistory = JsonSerializer.Deserialize<List<GameHistoryEntry>>(json, _loadOptions);

                if (gameHistory != null)
                {
                    var loadedHistory = new List<ActivityData>();

                    foreach (var entry in gameHistory)
                    {
                        if (entry.UniverseId == 0 || entry.PlaceId == 0) continue;

                        foreach (var server in entry.Servers)
                        {
                            if (server.JoinedAt == default) continue;

                            var activity = new ActivityData
                            {
                                UniverseId = entry.UniverseId,
                                PlaceId = entry.PlaceId,
                                JobId = server.JobId,
                                ServerType = server.ServerType,
                                TimeJoined = server.JoinedAt,
                                TimeLeft = server.TimeLeft,
                                Region = server.Region
                            };

                            activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                            loadedHistory.Add(activity);
                        }
                    }

                    History = [.. loadedHistory
                        .OrderByDescending(x => x.TimeJoined)
                        .Take(300)];

                    App.Logger.WriteLine("ActivityWatcher::LoadGameHistory", $"Loaded {History.Count} sessions from cache");
                }
                else
                {
                    History = [];
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ActivityWatcher::LoadGameHistory", ex);
                History = [];
            }
        }

        private async void AddToHistory(ActivityData activity)
        {
            if (activity.ServerType is ServerType.Private or ServerType.Reserved) return;
            if (activity.UniverseId == 0 || activity.PlaceId == 0 || activity.TimeJoined == default) return;

            if (activity.MachineAddressValid && string.IsNullOrEmpty(activity.Region))
            {
                activity.Region = await activity.QueryServerLocation() ?? "Unknown";
            }

            if (!string.IsNullOrEmpty(activity.JobId))
            {
                History.RemoveAll(x => x.JobId == activity.JobId);
            }

            History.Insert(0, activity);

            if (History.Count > 300)
            {
                History = [.. History.OrderByDescending(x => x.TimeJoined).Take(300)];
            }

            SaveGameHistory();
            OnHistoryUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void SaveGameHistory()
        {
            try
            {
                Directory.CreateDirectory(Paths.Cache);

                List<GameHistoryEntry> gameHistory = [.. History
                    .Where(a => a.UniverseId != 0 && a.PlaceId != 0)
                    .GroupBy(a => a.UniverseId)
                    .OrderByDescending(g => g.Max(s => s.TimeJoined))
                    .Take(30)
                    .Select(g => new GameHistoryEntry
                    {
                        UniverseId = g.Key,
                        PlaceId = g.OrderByDescending(s => s.TimeJoined).First().PlaceId,
                        Servers = [.. g.OrderByDescending(s => s.TimeJoined)
                                   .Take(10)
                                   .Select(s => new ServerInfo
                                   {
                                       JobId = s.JobId,
                                       JoinedAt = s.TimeJoined,
                                       TimeLeft = s.TimeLeft,
                                       ServerType = s.ServerType,
                                       Region = s.Region
                                   })]
                    })];

                string json = JsonSerializer.Serialize(gameHistory, _saveOptions);
                File.WriteAllText(GameHistoryCachePath, json);

                App.Logger.WriteLine("ActivityWatcher::SaveGameHistory", $"Saved {gameHistory.Count} games (max 10 servers each) to cache");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ActivityWatcher::SaveGameHistory", ex);
            }
        }

        public void Dispose()
        {
            IsDisposed = true;
            if (InRobloxStudio)
                StopHTTPServer();
            GC.SuppressFinalize(this);
        }
    }
}