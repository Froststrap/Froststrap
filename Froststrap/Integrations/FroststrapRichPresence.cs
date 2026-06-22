using System.Net.Sockets;
using DiscordRPC;

namespace Froststrap.Integrations
{
    public class FroststrapRichPresence : IDisposable
    {
        private readonly DiscordRpcClient _rpcClient;
        private readonly Timestamps _startTimestamps;
        private readonly Stopwatch _uptimeStopwatch;
        private bool _disposed = false;
        private string _currentPage = "Idle";
        private string? _currentDialog = null;
        private string _lastState = "";
        private readonly SemaphoreSlim _updateLock = new(1, 1);
        private bool _isReconnecting = false;
        private int _retryCount = 0;
        private const int MAX_RETRIES = 3;
        private readonly System.Timers.Timer? _reconnectTimer;

        public bool IsConnected => _rpcClient?.IsInitialized == true;

        public FroststrapRichPresence()
        {
            _rpcClient = new DiscordRpcClient("1399535282713399418")
            {
                SkipIdenticalPresence = true
            };

            _rpcClient.OnReady += OnReady;
            _rpcClient.OnError += OnError;
            _rpcClient.OnConnectionFailed += OnConnectionFailed;
            _rpcClient.OnClose += OnClose;

            _startTimestamps = new Timestamps
            {
                Start = DateTime.UtcNow
            };

            _uptimeStopwatch = Stopwatch.StartNew();

            InitializeRpcClient();

            _reconnectTimer = new System.Timers.Timer(30000);
            _reconnectTimer.Elapsed += (sender, e) => CheckConnection();
            _reconnectTimer.AutoReset = true;
            _reconnectTimer.Start();
        }

        private void InitializeRpcClient()
        {
            const string LOG_IDENT = "FroststrapRichPresence::InitializeRpcClient";

            try
            {
                _rpcClient.Initialize();
                _retryCount = 0;
                App.Logger.WriteLine(LOG_IDENT, "RPC client initialized successfully");
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Socket interrupted during initialization. Will retry...");
                ScheduleReconnect();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to init RPC: {ex.Message}");
                App.Logger.WriteException(LOG_IDENT, ex);
                ScheduleReconnect();
            }
        }

        private void ScheduleReconnect()
        {
            const string LOG_IDENT = "FroststrapRichPresence::ScheduleReconnect";

            if (_disposed || _isReconnecting) return;
            if (_retryCount >= MAX_RETRIES)
            {
                App.Logger.WriteLine(LOG_IDENT, "Max retries reached. Giving up on RPC connection.");
                return;
            }

            _retryCount++;
            App.Logger.WriteLine(LOG_IDENT, $"Scheduling reconnect attempt {_retryCount}/{MAX_RETRIES} in 5 seconds...");

            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                if (!_disposed && !_isReconnecting)
                {
                    await ReconnectRpcClient();
                }
            });
        }

        private void CheckConnection()
        {
            if (_disposed || _isReconnecting) return;

            try
            {
                if (!_rpcClient.IsInitialized)
                {
                    App.Logger.WriteLine("FroststrapRichPresence::CheckConnection", "Connection lost, attempting to reconnect...");
                    _ = ReconnectRpcClient();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FroststrapRichPresence::CheckConnection", $"Health check failed: {ex.Message}");
                _ = ReconnectRpcClient();
            }
        }

        private async Task ReconnectRpcClient()
        {
            const string LOG_IDENT = "FroststrapRichPresence::ReconnectRpcClient";

            if (_isReconnecting || _disposed)
                return;

            _isReconnecting = true;
            try
            {
                try
                {
                    if (_rpcClient.IsInitialized)
                    {
                        try
                        {
                            _rpcClient.ClearPresence();
                        }
                        catch (IOException) { /* Ignore */ }
                        _rpcClient.Dispose();
                    }
                }
                catch (IOException) { /* Ignore socket errors during cleanup */ }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Error during cleanup: {ex.Message}");
                }

                await Task.Delay(1000);

                try
                {
                    _rpcClient.Initialize();
                    _retryCount = 0;
                    App.Logger.WriteLine(LOG_IDENT, "RPC client reconnected successfully");

                    if (!_disposed)
                    {
                        UpdatePresence();
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to reconnect RPC client: {ex.Message}");
                    if (_retryCount < MAX_RETRIES)
                    {
                        ScheduleReconnect();
                    }
                }
            }
            finally
            {
                _isReconnecting = false;
            }
        }

        private void OnReady(object sender, DiscordRPC.Message.ReadyMessage args)
        {
            App.Logger.WriteLine("FroststrapRichPresence", $"Connected as {args.User.Username}");
            _retryCount = 0;

            if (!_disposed)
                UpdatePresence();
        }

        private void OnError(object sender, DiscordRPC.Message.ErrorMessage args)
        {
            const string LOG_IDENT = "FroststrapRichPresence::OnError";

            if (args.Message?.Contains("pipe", StringComparison.OrdinalIgnoreCase) == true)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Discord RPC pipe error (expected on macOS): {args.Message}");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Discord RPC Error: {args.Message} (Code: {args.Code})");

            string errorMsg = args.Message?.ToLower() ?? "";
            if (errorMsg.Contains("pipe") || errorMsg.Contains("connection") || errorMsg.Contains("failed"))
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (!_disposed && !_isReconnecting)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Attempting to reconnect after error...");
                        await ReconnectRpcClient();
                    }
                });
            }
        }

        private void OnConnectionFailed(object sender, DiscordRPC.Message.ConnectionFailedMessage args)
        {
            const string LOG_IDENT = "FroststrapRichPresence::OnConnectionFailed";
            App.Logger.WriteLine(LOG_IDENT, "Discord RPC Connection Failed");

            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                if (!_disposed && !_isReconnecting)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Attempting to reconnect after connection failure...");
                    await ReconnectRpcClient();
                }
            });
        }

        private void OnClose(object sender, DiscordRPC.Message.CloseMessage args)
        {
            const string LOG_IDENT = "FroststrapRichPresence::OnClose";
            App.Logger.WriteLine(LOG_IDENT, $"Discord RPC connection closed: {args.Reason} (Code: {args.Code})");

            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                if (!_disposed && !_isReconnecting)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Attempting to reconnect after close...");
                    await ReconnectRpcClient();
                }
            });
        }

        public void SetPage(string pageName)
        {
            if (_disposed) return;

            _currentPage = pageName;
            _currentDialog = null;
            UpdatePresence();
        }

        public void SetDialog(string dialogName)
        {
            if (_disposed) return;

            _currentDialog = dialogName;
            UpdatePresence();
        }

        public void ClearDialog()
        {
            if (_disposed) return;

            _currentDialog = null;
            UpdatePresence();
        }

        public void UpdatePresence()
        {
            const string LOG_IDENT = "FroststrapRichPresence::UpdatePresence";

            if (_disposed || !_rpcClient.IsInitialized)
                return;

            string state = !string.IsNullOrEmpty(_currentDialog)
                ? $"Page: {_currentPage} | Dialog: {_currentDialog}"
                : $"Page: {_currentPage}";

            if (state == _lastState)
                return;

            _lastState = state;

            var presence = new DiscordRPC.RichPresence
            {
                Details = "Customize Roblox to your liking!",
                State = state,
                Timestamps = _startTimestamps,
                Assets = new Assets
                {
                    LargeImageKey = "froststrap",
                    LargeImageText = "Froststrap"
                },
                Buttons =
                [
                    new Button { Label = "GitHub", Url = "https://github.com/Froststrap/Froststrap" },
                    new Button { Label = "Discord", Url = "https://discord.gg/KdR9vpRcUN" }
                ]
            };

            int maxRetries = 3;
            int retryDelay = 100;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    _rpcClient.SetPresence(presence);
                    break;
                }
                catch (IOException ex) when (ex.InnerException is SocketException)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Socket interrupted (Attempt {retry + 1}/{maxRetries}). Retrying...");
                    if (retry == maxRetries - 1)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Max retries reached. Giving up on this update.");
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(1000);
                            if (!_disposed && !_isReconnecting)
                            {
                                App.Logger.WriteLine(LOG_IDENT, "Attempting to reconnect after update failure...");
                                await ReconnectRpcClient();
                            }
                        });
                    }
                    else
                    {
                        Thread.Sleep(retryDelay * (retry + 1));
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            App.Logger.WriteLine("FroststrapRichPresence::Dispose", "Cleaning up Discord RPC");

            _reconnectTimer?.Stop();
            _reconnectTimer?.Dispose();

            try
            {
                _rpcClient.OnReady -= OnReady;
                _rpcClient.OnError -= OnError;
                _rpcClient.OnConnectionFailed -= OnConnectionFailed;
                _rpcClient.OnClose -= OnClose;

                if (_rpcClient.IsInitialized)
                {
                    try
                    {
                        Task.Delay(100).Wait(100);
                        _rpcClient.ClearPresence();
                    }
                    catch (IOException)
                    {
                        App.Logger.WriteLine("FroststrapRichPresence::Dispose", "Socket error during presence clear (ignored)");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("FroststrapRichPresence::Dispose", $"Error clearing presence: {ex.Message}");
                    }
                }

                _rpcClient.Dispose();
                _uptimeStopwatch.Stop();
            }
            catch (IOException)
            {
                App.Logger.WriteLine("FroststrapRichPresence::Dispose", "Socket error during disposal (ignored)");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FroststrapRichPresence::Dispose", $"Cleanup error: {ex.Message}");
                App.Logger.WriteException("FroststrapRichPresence::Dispose", ex);
            }
            finally
            {
                _updateLock?.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}