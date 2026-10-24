// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Net.Sockets;
using Froststrap.Integrations.Discord;

namespace Froststrap.Integrations.Discord
{
    internal class FroststrapRichPresence : IDisposable
    {
        private readonly DiscordRpcClient? _rpcClient;
        private readonly Timestamps _startTimestamps;
        private readonly Stopwatch _uptimeStopwatch;
        private bool _disposed;
        private string _currentPage = "Starting";
        private string? _currentDialog;
        private string _lastState = "";

        public bool IsConnected => _rpcClient?.IsInitialized == true;

        public FroststrapRichPresence()
        {
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

            _ = Task.Run(() =>
            {
                try
                {
                    _rpcClient.Initialize();
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to init RPC: {ex.Message}");
                }
            });
        }

        private void OnReady(object? sender, ReadyMessage args)
        {
            if (_disposed) return;

            App.Logger.Info($"Connected as {args.User.Username}");

            _ = PushInitialPresenceAsync();
        }

        private async Task PushInitialPresenceAsync()
        {
            var ackTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<PresenceUpdateMessage> handler = (_, _) => ackTcs.TrySetResult(true);

            if (_rpcClient != null)
                _rpcClient.OnPresenceUpdate += handler;

            try
            {
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    if (_disposed) return;

                    _lastState = "";
                    UpdatePresence();

                    var result = await Task.WhenAny(ackTcs.Task, Task.Delay(2000));
                    if (result == ackTcs.Task)
                        return;
                }

                App.Logger.Warn("Initial Froststrap presence not acknowledged after 3 attempts");
            }
            finally
            {
                if (_rpcClient != null)
                    _rpcClient.OnPresenceUpdate -= handler;
            }
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
            if (_disposed || _rpcClient == null || !_rpcClient.IsInitialized)
                return;

            string state = !string.IsNullOrEmpty(_currentDialog)
                ? $"Page: {_currentPage} | Dialog: {_currentDialog}"
                : $"Page: {_currentPage}";

            if (state == _lastState)
                return;

            _lastState = state;

            var presence = new DiscordActivity
            {
                Details = "Customize Roblox to your liking!",
                State = state,
                Timestamps = _startTimestamps,
                Assets = new Assets
                {
                    LargeImageKey = "froststrap",
                    LargeImageText = "Froststrap",
                    SmallImageKey = "checkmark",
                    SmallImageText = $"{App.Version}"
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
                App.Logger.Error("Socket interrupted (Operation Canceled).");
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

            if (_rpcClient != null)
            {
                try
                {
                    _rpcClient.OnReady -= OnReady;
                    _rpcClient.Dispose();
                }
                catch (IOException ex) when (ex.InnerException is SocketException)
                {
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