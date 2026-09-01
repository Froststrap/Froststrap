using System.Net.Sockets;
using DiscordRPC;

namespace Froststrap.Integrations
{
    internal class FroststrapRichPresence : IDisposable
    {
        private readonly DiscordRpcClient? _rpcClient;
        private readonly Timestamps _startTimestamps;
        private readonly Stopwatch _uptimeStopwatch;
        private bool _disposed;
        private string _currentPage = "Idle";
        private string? _currentDialog;
        private string _lastState = "";
        private readonly bool _isMacOS;

        public bool IsConnected => _rpcClient?.IsInitialized == true;

        public FroststrapRichPresence()
        {
            _isMacOS = OperatingSystem.IsMacOS();

            if (_isMacOS)
            {
                App.Logger.Warn("Skipping Discord RPC initialization on macOS");
                _rpcClient = null!;
                _startTimestamps = new Timestamps { Start = DateTime.UtcNow };
                _uptimeStopwatch = Stopwatch.StartNew();
                return;
            }

            _rpcClient = new DiscordRpcClient("1399535282713399418")
            {
                SkipIdenticalPresence = true
            };

            _rpcClient.OnReady += OnReady;

            _startTimestamps = new Timestamps
            {
                Start = DateTime.UtcNow
            };

            _uptimeStopwatch = Stopwatch.StartNew();

            try
            {
                _rpcClient.Initialize();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to init RPC: {ex.Message}");
            }
        }

        private void OnReady(object sender, DiscordRPC.Message.ReadyMessage args)
        {
            if (_disposed || _isMacOS) return;

            App.Logger.Info($"Connected as {args.User.Username}");

            if (!_disposed)
                UpdatePresence();
        }

        public void SetPage(string pageName)
        {
            if (_disposed || _isMacOS) return;

            _currentPage = pageName;
            _currentDialog = null;
            UpdatePresence();
        }

        public void SetDialog(string dialogName)
        {
            if (_disposed || _isMacOS) return;

            _currentDialog = dialogName;
            UpdatePresence();
        }

        public void ClearDialog()
        {
            if (_disposed || _isMacOS) return;

            _currentDialog = null;
            UpdatePresence();
        }

        public void UpdatePresence()
        {
            if (_disposed || _isMacOS || _rpcClient == null || !_rpcClient.IsInitialized)
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
                    LargeImageText = "Froststrap",
                    SmallImageKey = "checkmark",
                    SmallImageText = $"v{App.Version}"
                },
                Buttons =
                [
                    new Button { Label = "GitHub", Url = "https://github.com/Froststrap/Froststrap" },
                    new Button { Label = "Discord", Url = "https://discord.gg/KdR9vpRcUN" }
                ]
            };

            try
            {
                _rpcClient.SetPresence(presence);
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                App.Logger.Error("Socket interrupted (Operation Canceled). This is expected on macOS.");
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception: ", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            App.Logger.Info("Cleaning up Discord RPC");

            if (_rpcClient != null)
            {
                try
                {
                    _rpcClient.OnReady -= OnReady;

                    if (_rpcClient.IsInitialized)
                    {
                        try
                        {
                            _rpcClient.ClearPresence();
                        }
                        catch (IOException) { /* Ignore pipe closure issues */ }
                    }

                    _rpcClient.Dispose();
                }
                catch (IOException ex) when (ex.InnerException is SocketException)
                {
                    // Ignore
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Cleanup error: {ex.Message}");
                }
            }

            _uptimeStopwatch.Stop();
            GC.SuppressFinalize(this);
        }
    }
}
