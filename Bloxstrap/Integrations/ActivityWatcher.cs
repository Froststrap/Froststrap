namespace Bloxstrap.Integrations
{
    public class ActivityWatcher : IDisposable
    {
        private const string GameMessageEntry = "[FLog::Output] [BloxstrapRPC]";
        private const string GameJoiningEntry = "[FLog::Output] ! Joining game";

        // these entries are technically volatile!
        // they only get printed depending on their configured FLog level, which could change at any time
        // while levels being changed is fairly rare, please limit the number of varying number of FLog types you have to use, if possible

        private const string GameTeleportingEntry = "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToPlace";
        private const string GameJoiningPrivateServerEntry = "[FLog::GameJoinUtil] GameJoinUtil::joinGamePostPrivateServer";
        private const string GameJoiningReservedServerEntry = "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToReservedServer";
        private const string GameJoiningUniverseEntry = "[FLog::GameJoinLoadTime] Report game_join_loadtime:";
        private const string GameJoiningUDMUXEntry = "[FLog::Network] UDMUX Address = ";
        private const string GameJoinedEntry = "[FLog::Network] serverId:";
        private const string GameDisconnectedEntry = "[FLog::Network] Time to disconnect replication data:";
        private const string GameLeavingEntry = "[FLog::SingleSurfaceApp] leaveUGCGameInternal";
        private const string GameDisconnectReasonEntry = "[FLog::Network] Sending disconnect with reason:";

        private const string StudioPlaceOpenEntry = "[FLog::PlaceManager] Start to open place";
        private const string StudioPlaceCloseEntry = "[FLog::PlaceManager] PlaceManager::closeCurrentPlayDoc";

        private const string GameJoiningEntryPattern = @"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)";
        private const string GameJoiningPrivateServerPattern = @"""accessCode"":""([0-9a-f\-]{36})""";
        private const string GameJoiningUniversePattern = @"universeid:([0-9]+).*userid:([0-9]+)";
        private const string GameJoiningUDMUXPattern = @"UDMUX Address = ([0-9\.]+), Port = [0-9]+ \| RCC Server Address = ([0-9\.]+), Port = [0-9]+";
        private const string GameJoinedEntryPattern = @"serverId: ([0-9\.]+)\|[0-9]+";
        private const string GameMessageEntryPattern = @"\[BloxstrapRPC\] (.*)";
        private const string GameDisconnectReasonPattern = @"Sending disconnect with reason: (\d+)";

        private int _logEntriesRead = 0;
        private bool _teleportMarker = false;
        private bool _reservedTeleportMarker = false;
        private bool _shouldAutoRejoin = false;

        private static readonly string GameHistoryCachePath = Path.Combine(Paths.Cache, "GameHistory.json");
        public event EventHandler? OnHistoryUpdated;

        public event EventHandler<string>? OnLogEntry;
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
        private Thread? _httpListenerThread;
        private bool _isHttpListenerRunning = false;
        private readonly CancellationTokenSource _httpCancellationTokenSource = new();

        public ActivityData Data { get; private set; } = new();

        /// <summary>
        /// Ordered by newest to oldest
        /// </summary>
        public List<ActivityData> History = new();

        public bool IsDisposed = false;

        public void CloseProcess(int pid)
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
                string logDirectory = Path.Combine(Paths.Roblox, "logs");

                if (!Directory.Exists(logDirectory))
                    return;

                // we need to make sure we're fetching the absolute latest log file
                // if roblox doesn't start quickly enough, we can wind up fetching the previous log file
                // good rule of thumb is to find a log file that was created in the last 15 seconds or so

                App.Logger.WriteLine(LOG_IDENT, "Opening Roblox log file...");

                while (true)
                {
                    logFileInfo = new DirectoryInfo(logDirectory)
                        .GetFiles()
                        .Where(x => x.Name.Contains("Player", StringComparison.OrdinalIgnoreCase) && x.CreationTime <= DateTime.Now)
                        .OrderByDescending(x => x.CreationTime)
                        .First();

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

            // get the log message from the read line
            int logMessageIdx = entry.IndexOf(' ');
            if (logMessageIdx == -1)
            {
                // likely a log message that spanned multiple lines
                return;
            }

            string logMessage = entry[(logMessageIdx + 1)..];

            if (InRobloxStudio || _launchMode == LaunchMode.Studio || _launchMode == LaunchMode.StudioAuth)
            {
                ProcessStudioLogEntry(logMessage);
            }
            else
            {
                ProcessPlayerLogEntry(logMessage);
            }
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

            if (logMessage.StartsWith(GameLeavingEntry))
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

                if (logMessage.StartsWith(GameJoiningPrivateServerEntry))
                {
                    // we only expect to be joining a private server if we're not already in a game

                    Data.ServerType = ServerType.Private;

                    var match = Regex.Match(logMessage, GameJoiningPrivateServerPattern);

                    if (match.Groups.Count != 2)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to assert format for game join private server entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    Data.AccessCode = match.Groups[1].Value;
                }
                else if (logMessage.StartsWith(GameJoiningEntry))
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

                    if (App.Settings.Prop.ShowServerDetails && Data.MachineAddressValid)
                        _ = Data.QueryServerLocation();

                    if (App.Settings.Prop.ShowServerUptime && Data.JobId != null)
                        _ = Data.QueryServerTime();

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

                if (logMessage.StartsWith(GameJoiningUniverseEntry))
                {
                    var match = Regex.Match(logMessage, GameJoiningUniversePattern);

                    if (match.Groups.Count != 3)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to assert format for game join universe entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    Data.UniverseId = Int64.Parse(match.Groups[1].Value);
                    Data.UserId = Int64.Parse(match.Groups[2].Value);

                    if (History.Any())
                    {
                        var lastActivity = History.First();

                        if (Data.UniverseId == lastActivity.UniverseId && Data.IsTeleport)
                            Data.RootActivity = lastActivity.RootActivity ?? lastActivity;
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

                    if (App.Settings.Prop.ShowServerDetails)
                        _ = Data.QueryServerLocation();

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
                }
                else if (logMessage.StartsWith(GameJoiningReservedServerEntry))
                {
                    _teleportMarker = true;
                    _reservedTeleportMarker = true;
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
            }
        }

        private void StartHTTPServer()
        {
            try
            {
        	    if (_httpListener != null && _httpListener.IsListening)
        	    {
        	        App.Logger.WriteLine("ActivityWatcher::StartHTTPServer", "HTTP server is already running");
        	        return;
        	    }

                _httpListener = new HttpListener();

                _httpListener.Prefixes.Add($"http://localhost:{HttpPort}/");

                _httpListener.Start();

                _isHttpListenerRunning = true;
                _httpListenerThread = new Thread(() => ListenForHTTPRequests(_httpCancellationTokenSource.Token));
                _httpListenerThread.IsBackground = true;
                _httpListenerThread.Name = "StudioRPC-HTTP-Listener";
                _httpListenerThread.Start();

                App.Logger.WriteLine("ActivityWatcher::StartHTTPServer", $"HTTP server started on port {HttpPort}");
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 183) // ERROR_ALREADY_EXISTS
            {
                App.Logger.WriteLine("ActivityWatcher::StartHTTPServer", $"Port {HttpPort} is already in use by another process");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ActivityWatcher::StartHTTPServer", ex);
            }
        }

        private async void ListenForHTTPRequests(CancellationToken cancellationToken)
        {
            while (_isHttpListenerRunning && _httpListener != null && _httpListener.IsListening && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync().WaitAsync(cancellationToken);
                    _ = Task.Run(() => ProcessHTTPRequest(context), cancellationToken);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("ActivityWatcher::ListenForHTTPRequests", ex);
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private void ProcessHTTPRequest(HttpListenerContext context)
        {
            const string LOG_IDENT = "ActivityWatcher::ProcessHTTPRequest";

            try
            {
                if (context.Request.HttpMethod == "POST" && context.Request.Url?.AbsolutePath == "/rpc")
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        App.Logger.WriteLine(LOG_IDENT, $"Received HTTP RPC: {json}");

                        var studioMessage = JsonSerializer.Deserialize<StudioMessage>(json);

                        if (studioMessage != null)
                        {
                            if (studioMessage.StudioCommand == "SetRichPresence")
                            {
                                var richPresenceData = studioMessage.Data.Deserialize<StudioRichPresence>();
                                if (richPresenceData != null)
                                {
                                    var fullMessage = new StudioMessage
                                    {
                                        StudioCommand = studioMessage.StudioCommand,
                                        Data = JsonSerializer.SerializeToElement(richPresenceData)
                                    };
                                    OnStudioRPCMessage?.Invoke(this, fullMessage);
                                }
                            }
                            else
                            {
                                OnStudioRPCMessage?.Invoke(this, studioMessage);
                            }
                        }
                        else
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse JSON message");
                        }
                    }

                    context.Response.StatusCode = 200;
                    context.Response.Close();
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
            catch (JsonException jsonEx)
            {
                App.Logger.WriteLine(LOG_IDENT, $"JSON parsing error: {jsonEx.Message}");
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        private void StopHTTPServer()
        {
            _isHttpListenerRunning = false;
            _httpCancellationTokenSource.Cancel();

            if (_httpListener != null)
            {
                try
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("ActivityWatcher::StopHTTPServer", ex);
                }
                _httpListener = null;
            }

            if (_httpListenerThread != null && _httpListenerThread.IsAlive)
            {
                _httpListenerThread.Join(2000);
            }

            _httpCancellationTokenSource.Dispose();
        }

        public void LoadGameHistory()
        {
            try
            {
                if (!File.Exists(GameHistoryCachePath))
                {
                    App.Logger.WriteLine("ActivityWatcher::LoadGameHistory", "No existing game history cache found");
                    History = new List<ActivityData>();
                    return;
                }

                string json = File.ReadAllText(GameHistoryCachePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                };

                var gameHistory = JsonSerializer.Deserialize<List<GameHistoryData>>(json, options);

                if (gameHistory != null)
                {
                    History = new List<ActivityData>();

                    foreach (var history in gameHistory)
                    {
                        var serverType = (ServerType)history.ServerType;

                        if (serverType == ServerType.Private || serverType == ServerType.Reserved)
                        {
                            continue;
                        }

                        if (history.UniverseId == 0 || history.PlaceId == 0 || history.TimeJoined == default)
                        {
                            continue;
                        }

                        var activity = new ActivityData
                        {
                            UniverseId = history.UniverseId,
                            PlaceId = history.PlaceId,
                            JobId = history.JobId,
                            UserId = history.UserId,
                            ServerType = serverType,
                            TimeJoined = history.TimeJoined,
                            TimeLeft = history.TimeLeft,
                        };

                        activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                        History.Add(activity);
                    }

                    History = History
                        .GroupBy(x => x.UniverseId)
                        .SelectMany(g => g.OrderByDescending(x => x.TimeJoined).Take(3))
                        .OrderByDescending(x => x.TimeJoined)
                        .ToList();

                    App.Logger.WriteLine("ActivityWatcher::LoadGameHistory", $"Loaded {History.Count} game history entries from cache");
                }
                else
                {
                    History = new List<ActivityData>();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ActivityWatcher::LoadGameHistory", ex);
                History = new List<ActivityData>();
            }
        }

        public void SaveGameHistory()
        {
            try
            {
                Directory.CreateDirectory(Paths.Cache);

                var validHistory = History
                    .Where(activity =>
                        activity.ServerType != ServerType.Private &&
                        activity.ServerType != ServerType.Reserved &&
                        activity.UniverseId != 0 &&
                        activity.PlaceId != 0 &&
                        activity.TimeJoined != default)
                    .ToList();

                var limitedHistory = validHistory
                    .GroupBy(x => x.UniverseId)
                    .SelectMany(g => g.OrderByDescending(x => x.TimeJoined).Take(3))
                    .ToList();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var gameHistory = limitedHistory.Select(activity => new GameHistoryData
                {
                    UniverseId = activity.UniverseId,
                    PlaceId = activity.PlaceId,
                    JobId = activity.JobId,
                    UserId = activity.UserId,
                    ServerType = (int)activity.ServerType,
                    TimeJoined = activity.TimeJoined,
                    TimeLeft = activity.TimeLeft,
                }).ToList();

                string json = JsonSerializer.Serialize(gameHistory, options);
                File.WriteAllText(GameHistoryCachePath, json);

                App.Logger.WriteLine("ActivityWatcher::SaveGameHistory",
                    $"Saved {gameHistory.Count} game history entries to cache ({limitedHistory.Count} after filtering)");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ActivityWatcher::SaveGameHistory", ex);
            }
        }

        private void AddToHistory(ActivityData activity)
        {
            if (activity.ServerType == ServerType.Private || activity.ServerType == ServerType.Reserved)
            {
                App.Logger.WriteLine("ActivityWatcher::AddToHistory",
                    $"Skipping {activity.ServerType} server from history");
                return;
            }

            if (activity.UniverseId == 0 || activity.PlaceId == 0 || activity.TimeJoined == default)
            {
                App.Logger.WriteLine("ActivityWatcher::AddToHistory",
                    "Skipping incomplete activity from history");
                return;
            }

            if (!string.IsNullOrEmpty(activity.JobId))
            {
                History.RemoveAll(x => x.JobId == activity.JobId);
            }

            History.Insert(0, activity);

            History = History
                .GroupBy(x => x.UniverseId)
                .SelectMany(g => g.OrderByDescending(x => x.TimeJoined).Take(3))
                .OrderByDescending(x => x.TimeJoined)
                .ToList();

            if (History.Count > 125)
            {
                History = History.Take(125).ToList();
            }

            SaveGameHistory();
            OnHistoryUpdated?.Invoke(this, EventArgs.Empty);

            App.Logger.WriteLine("ActivityWatcher::AddToHistory",
                $"Added history entry for universe {activity.UniverseId}. Total entries: {History.Count}");
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