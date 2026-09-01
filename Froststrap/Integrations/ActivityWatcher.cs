using System.Runtime.InteropServices;

namespace Froststrap.Integrations
{
    internal class ActivityWatcher : IDisposable
    {
        private const string GameMessageEntry = "[FLog::CreatorOutput] [BloxstrapRPC]";
        private const string GameJoiningEntry = "[FLog::Output] ! Joining game";
        private const string GameTeleportingEntry = "[FLog::UgcExperienceController] UgcExperienceController: doTeleport: joinScriptUrl";
        private const string GameLaunchEventEntry = "[FLog::NewWebView2Browser] Webview handles hybrid javascript event";
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
        private const string GameJoiningUDMUXPattern = @"UDMUX Address\s*=\s*([0-9\.]+),\s*Port\s*=\s*[0-9]+\s*\|\s*RCC Server Address\s*=\s*([0-9\.]+),\s*Port\s*=\s*[0-9]+";
        private const string GameJoinedEntryPattern = @"serverId: ([0-9\.]+)\|[0-9]+";
        private const string GameMessageEntryPattern = @"\[BloxstrapRPC\] (.*)";
        private const string GameDisconnectReasonPattern = @"Sending disconnect with reason: (\d+)";
        private const string GameServerUptimePattern = @"Server Prefix:.+_(\d{8}T\d{6}Z)_RCC_[0-9a-z]+";

        private int _logEntriesRead;
        private bool _teleportMarker;
        private bool _reservedTeleportMarker;
        private bool _shouldAutoRejoin;

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

        public bool InGame;
        public bool InStudioPlace;
        public bool InRobloxStudio;

        private const int HttpPort = 4875;
        private HttpListener? _httpListener;
        private readonly CancellationTokenSource _httpCancellationTokenSource = new();

        public ActivityData Data { get; private set; } = new();

        /// <summary>
        /// Ordered by newest to oldest
        /// </summary>
        public List<ActivityData> History = [];

        public bool IsDisposed;

        public static void CloseProcess(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    App.Logger.Info($"PID {pid} has already exited");
                    return;
                }

                process.Kill();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"PID {pid} could not be closed {ex}");
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
            FileInfo logFileInfo;

            if (String.IsNullOrEmpty(LogLocation))
            {
                string logDirectory = Paths.RobloxLogs;

                if (!Directory.Exists(logDirectory))
                    return;

                App.Logger.Info("Opening Roblox log file...");

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
                        App.Logger.Info($"No '{logNameFilter}' log files found, waiting...");
                        await Task.Delay(1000);
                        continue;
                    }

                    logFileInfo = candidates.First();

                    if (logFileInfo.CreationTime.AddSeconds(15) > DateTime.Now)
                        break;

                    App.Logger.Info($"Could not find recent enough log file, waiting... (newest is {logFileInfo.Name})");
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

            App.Logger.Info($"Opened {LogLocation}");

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
            OnLogEntry?.Invoke(this, entry);

            _logEntriesRead += 1;

            if (_logEntriesRead <= 1000 && _logEntriesRead % 50 == 0)
                App.Logger.Info($"Read {_logEntriesRead} log entries");
            else if (_logEntriesRead % 100 == 0)
                App.Logger.Info($"Read {_logEntriesRead} log entries");

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

            int logMessageIdx = entry.IndexOf(' ', StringComparison.Ordinal);
            if (logMessageIdx == -1)
                return null;

            return entry[(logMessageIdx + 1)..];
        }

        private void ProcessStudioLogEntry(string logMessage)
        {
            if (!InRobloxStudio)
            {
                InRobloxStudio = true;
            }

            if (!InStudioPlace)
            {
                if (logMessage.StartsWith(StudioPlaceOpenEntry, StringComparison.Ordinal))
                {
                    App.Logger.Info("Studio place opened");
                    InStudioPlace = true;

                    OnStudioPlaceOpened?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (InStudioPlace)
            {
                if (logMessage.StartsWith(StudioPlaceCloseEntry, StringComparison.Ordinal))
                {
                    App.Logger.Info("Studio place closed");
                    InStudioPlace = false;

                    OnStudioPlaceClosed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private async void ProcessPlayerLogEntry(string logMessage)
        {
            if (logMessage.StartsWith(GameLeavingEntry, StringComparison.Ordinal) ||
                logMessage.StartsWith(GameLeavingEntrySober, StringComparison.Ordinal) ||
                logMessage.StartsWith(AppCloseEntrySober, StringComparison.Ordinal))
            {
                App.Logger.Debug("User is back into the desktop app");

                OnAppClose?.Invoke(this, EventArgs.Empty);

                if (Data.PlaceId != 0 && !InGame)
                {
                    App.Logger.Debug("User appears to be leaving from a cancelled/errored join");
                    Data = new();
                }

                return;
            }

            if (logMessage.StartsWith(GameDisconnectReasonEntry, StringComparison.Ordinal))
            {
                var match = Regex.Match(logMessage, GameDisconnectReasonPattern);
                if (match.Success && match.Groups.Count == 2)
                {
                    int reasonCode = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

                    if (reasonCode == 1)
                    {
                        _shouldAutoRejoin = true;
                        App.Logger.Info($"Inactivity timeout detected (reason code: {reasonCode})");
                    }
                    if (reasonCode == 277)
                    {
                        _shouldAutoRejoin = true;
                        App.Logger.Info($"Internet Disconnection detected (reason code: {reasonCode})");
                    }
                    else
                    {
                        App.Logger.Info($"Disconnect reason code: {reasonCode}");
                    }
                }
            }

            if (!InGame && Data.PlaceId == 0)
            {
                if (logMessage.StartsWith(GameJoiningEntry, StringComparison.Ordinal))
                {
                    Match match = Regex.Match(logMessage, GameJoiningEntryPattern);

                    if (match.Groups.Count != 4)
                    {
                        App.Logger.Error($"Failed to assert format for game join entry {logMessage}");
                        return;
                    }

                    InGame = false;
                    Data.PlaceId = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
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

                    App.Logger.Info($"Joining Game ({Data})");
                }
                else if (logMessage.StartsWith(GameLaunchEventEntry, StringComparison.Ordinal))
                {
                    int jsonStart = logMessage.IndexOf('{', StringComparison.Ordinal);
                    string jsonString = logMessage[jsonStart..];
                    using JsonDocument doc = JsonDocument.Parse(jsonString);
                    if (doc.RootElement.TryGetProperty("params", out JsonElement paramsElement) && paramsElement.TryGetProperty("request", out JsonElement requestElement))
                    {
                        if (requestElement.TryGetProperty("requestType", out JsonElement requestTypeElement) && requestTypeElement.GetString() == "RequestPrivateGame")
                        {
                            if (requestElement.TryGetProperty("accessCode", out JsonElement accessCodeElement))
                            {
                                string? accessCode = accessCodeElement.GetString();
                                if (!string.IsNullOrEmpty(accessCode))
                                {
                                    Data.AccessCode = accessCode;
                                    Data.ServerType = ServerType.Private;
                                    App.Logger.Info($"Captured private server access code: {accessCode}");
                                }
                            }
                        }
                    }
                }
            }
            else if (!InGame && Data.PlaceId != 0)
            {
                if (logMessage.Contains(GameJoiningUniverseEntry, StringComparison.Ordinal))
                {
                    var universeMatch = Regex.Match(logMessage, GameJoiningUniversePattern, RegexOptions.IgnoreCase);
                    if (universeMatch.Success)
                    {
                        Data.UniverseId = long.Parse(universeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    }

                    var userMatch = Regex.Match(logMessage, GameJoiningUniverseUserIDPattern, RegexOptions.IgnoreCase);
                    if (userMatch.Success)
                    {
                        Data.UserId = long.Parse(userMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    }

                    if (Data.UniverseId == 0)
                    {
                        App.Logger.Error($"Failed to extract UniverseId from game join entry. {logMessage}");
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
                else if (logMessage.StartsWith(GameJoiningUDMUXEntry, StringComparison.Ordinal))
                {
                    var match = Regex.Match(logMessage, GameJoiningUDMUXPattern);

                    if (!match.Success || match.Groups.Count != 3)
                    {
                        App.Logger.Error($"Failed to parse UDMUX entry (regex mismatch). {logMessage}");
                        return;
                    }

                    string rccAddress = match.Groups[2].Value;

                    if (string.IsNullOrEmpty(Data.MachineAddress) || Data.MachineAddress != rccAddress)
                    {
                        App.Logger.Info($"Updating MachineAddress from {Data.MachineAddress} to {rccAddress}");
                        Data.MachineAddress = rccAddress;
                    }

                    App.Logger.Info($"Server is UDMUX protected ({Data})");
                }
                else if (logMessage.StartsWith(GameJoinedEntry, StringComparison.Ordinal))
                {
                    if (logMessage.Contains("UNASSIGNED_SYSTEM_ADDRESS", StringComparison.Ordinal))
                        return;

                    Match match = Regex.Match(logMessage, GameJoinedEntryPattern);

                    if (match.Success && match.Groups.Count == 2)
                    {
                        string serverAddress = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(serverAddress) && serverAddress != Data.MachineAddress)
                        {
                            App.Logger.Info($"Updating MachineAddress from {Data.MachineAddress} to {serverAddress}");
                            Data.MachineAddress = serverAddress;
                        }

                        App.Logger.Info($"Joined Game ({Data})");
                        InGame = true;
                        Data.TimeJoined = DateTime.Now;
                        OnGameJoin?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        App.Logger.Error($"Failed to assert format for game joined entry {logMessage}");
                    }
                }
            }
            else if (InGame && Data.PlaceId != 0)
            {
                if (logMessage.StartsWith(GameDisconnectedEntry, StringComparison.Ordinal))
                {
                    App.Logger.Info($"Disconnected from Game ({Data})");

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
                            CloseProcess(_robloxPID);
                        }
                        else
                        {
                            App.Logger.Warn("No inactivity detected within 3 seconds, skipping auto-rejoin");
                        }
                    }

                    _shouldAutoRejoin = false;
                }
                else if (logMessage.StartsWith(GameTeleportingEntry, StringComparison.Ordinal))
                {
                    App.Logger.Info($"Initiating teleport to server ({Data})");
                    _teleportMarker = true;

                    var joinTypeMatch = Regex.Match(logMessage, GameTeleportJoinTypePattern);
                    if (joinTypeMatch.Success && int.TryParse(joinTypeMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int joinTypeId))
                    {
                        var joinType = (ServerSessionJoinType)joinTypeId;
                        App.Logger.Info($"Teleport JoinTypeId: {joinTypeId}");

                        if (joinType is ServerSessionJoinType.NewGamePrivateGame or ServerSessionJoinType.SpecificPrivateGame)
                        {
                            _reservedTeleportMarker = true;
                            App.Logger.Info("Detected reserved server teleport");
                        }
                    }
                    else
                        App.Logger.Error("Failed to detect teleport type");
                }
                else if (logMessage.StartsWith(GameMessageEntry, StringComparison.Ordinal))
                {
                    var match = Regex.Match(logMessage, GameMessageEntryPattern);

                    if (match.Groups.Count != 2)
                    {
                        App.Logger.Error($"Failed to assert format for RPC message entry. {logMessage}");
                        return;
                    }

                    string messagePlain = match.Groups[1].Value;
                    Message? message;

                    App.Logger.Info($"Received message: '{messagePlain}'");

                    if ((DateTime.Now - LastRPCRequest).TotalSeconds <= 1)
                    {
                        App.Logger.Info("Dropping message as ratelimit has been hit");
                        return;
                    }

                    try
                    {
                        message = JsonSerializer.Deserialize<Message>(messagePlain);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Failed to parse message! (JSON deserialization threw an exception) {ex.Message}");
                        return;
                    }

                    if (message is null)
                    {
                        App.Logger.Error("Failed to parse message! (JSON deserialization returned null)");
                        return;
                    }

                    if (string.IsNullOrEmpty(message.Command))
                    {
                        App.Logger.Error("Failed to parse message! (Command is empty)");
                        return;
                    }

                    if (message.Command == "SetLaunchData")
                    {
                        string? data;

                        try
                        {
                            data = message.Data.Deserialize<string>();
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Error($"Failed to parse message! (JSON deserialization threw an exception) {ex.Message}");
                            return;
                        }

                        if (data is null)
                        {
                            App.Logger.Error("Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (data.Length > 200)
                        {
                            App.Logger.Error("Data cannot be longer than 200 characters");
                            return;
                        }

                        Data.RPCLaunchData = data;
                    }

                    OnRPCMessage?.Invoke(this, message);

                    LastRPCRequest = DateTime.Now;
                }
                else if (logMessage.StartsWith(GameServerUptimeEntry, StringComparison.Ordinal))
                {
                    Match match = Regex.Match(logMessage, GameServerUptimePattern);

                    if (!match.Success && match.Groups.Count == 2)
                    {
                        App.Logger.Error($"Failed to assert format for server uptime entry. {logMessage}");
                        return;
                    }

                    string startTime = match.Groups[1].Value;

                    App.Logger.Info($"Server started at {startTime}");

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

                App.Logger.Info($"Studio RPC server active on port {HttpPort}");
            }
            catch (Exception ex) { App.Logger.Error("Unhandled exception: ", ex); }
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
                    App.Logger.Error(ex);
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
                App.Logger.Error($"Unhandled exception: {ex.Message}");
                response.StatusCode = 500;
            }
        }

        public void LoadGameHistory()
        {
            try
            {
                if (!File.Exists(GameHistoryCachePath))
                {
                    App.Logger.Info("No existing game history cache found");
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

                    App.Logger.Info($"Loaded {History.Count} sessions from cache");
                }
                else
                {
                    History = [];
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
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

                App.Logger.Info($"Saved {gameHistory.Count} games (max 10 servers each) to cache");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
            }
        }

        public void Dispose()
        {
            IsDisposed = true;
            if (InRobloxStudio)
                StopHTTPServer();
            _httpCancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}