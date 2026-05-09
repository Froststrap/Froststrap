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

        public bool IsConnected => _rpcClient?.IsInitialized == true;

        public FroststrapRichPresence()
        {
            const string LOG_IDENT = "FroststrapRichPresence";

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
                App.Logger.WriteLine(LOG_IDENT, $"Failed to init RPC: {ex.Message}");
            }
        }

        private void OnReady(object sender, DiscordRPC.Message.ReadyMessage args)
        {
            App.Logger.WriteLine("FroststrapRichPresence", $"Connected as {args.User.Username}");

            if (!_disposed)
                UpdatePresence();
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
            const string LOG_IDENT = "FroststrapRichPresence";

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
                App.Logger.WriteLine(LOG_IDENT, "Socket interrupted (Operation Canceled). This is expected on macOS.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            App.Logger.WriteLine("FroststrapRichPresence::Dispose", "Cleaning up Discord RPC");

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
                _uptimeStopwatch.Stop();
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FroststrapRichPresence::Dispose", $"Cleanup error: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }
    }
}