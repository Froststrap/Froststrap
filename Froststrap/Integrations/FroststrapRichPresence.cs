using DiscordRPC;

namespace Bloxstrap.Integrations
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
            _rpcClient = new DiscordRpcClient("1399535282713399418")
            {
                SkipIdenticalPresence = true
            };

            _rpcClient.OnReady += OnReady;

            Task.Run(InitializeAsync);

            _startTimestamps = new Timestamps
            {
                Start = DateTime.UtcNow
            };

            _uptimeStopwatch = Stopwatch.StartNew();
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (!_rpcClient.Initialize())
                    return;

                await Task.Delay(100);
                SetPresence();
            }
            catch
            {
                // Silent fail
            }
        }

        private void OnReady(object sender, DiscordRPC.Message.ReadyMessage args)
        {
            App.Logger.WriteLine("FroststrapRichPresence", $"Connected as {args.User.Username}");
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

        public void ResetPresence()
        {
            if (_disposed) return;

            _currentPage = "Idle";
            _currentDialog = null;
            UpdatePresence();
        }

        private void SetPresence()
        {
            UpdatePresence();
        }

        private void UpdatePresence()
        {
            if (_disposed || !_rpcClient.IsInitialized)
                return;

            string state = !string.IsNullOrEmpty(_currentDialog)
                ? $"Page: {_currentPage} | Dialog: {_currentDialog}"
                : $"Page: {_currentPage}";

            if (state == _lastState)
                return;

            _lastState = state;

            try
            {
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
                    Buttons = new[]
                    {
                        new Button { Label = "GitHub", Url = "https://github.com/RealMeddsam/Froststrap" },
                        new Button { Label = "Discord", Url = "https://discord.gg/KdR9vpRcUN" }
                    }
                };

                _rpcClient.SetPresence(presence);
            }
            catch
            {
                // Silent fail
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _rpcClient.OnReady -= OnReady;

                if (_rpcClient.IsInitialized)
                {
                    _rpcClient.ClearPresence();
                }

                _rpcClient.Dispose();
                _uptimeStopwatch.Stop();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }
            catch
            {
                // Silent fail
            }

            GC.SuppressFinalize(this);
        }
    }
}