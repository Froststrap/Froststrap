// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

using Froststrap.Enums;
using Froststrap.Integrations.Overlay.Platform.Mac;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.Overlay;
using Froststrap.UI.Elements.Overlay;

namespace Froststrap.Integrations.Overlay.Platform
{
    internal sealed class MacOverlayHost : IOverlayHost
    {
        private const int ResolveTimeoutMs = 30_000;
        private const int ResolvePollMs = 500;

        public event EventHandler<OverlayBounds>? BoundsChanged;
        public event EventHandler<bool>? GameVisibilityChanged;
        public event EventHandler? WindowClosed;
        public event EventHandler<int>? HotkeyPressed;

        public RealtimeMessaging Messaging { get; } = new();
        public ActivityWatcher? ActivityWatcher { get; }
        public FriendPresence Friends { get; }
        public int ProcessId { get; }

        private readonly MacWindowTracker _tracker;
        private readonly Dictionary<Window, MacWindow> _overlayWindows = [];

        private GameOverlay? _window;
        private bool _friendsWatched;
        private bool _disposed;
        private volatile bool _gameForeground;

        public MacOverlayHost(int robloxProcessId, ActivityWatcher? activityWatcher)
        {
            ProcessId = robloxProcessId;
            ActivityWatcher = activityWatcher;
            Friends = new FriendPresence(activityWatcher, Messaging);

            _tracker = new MacWindowTracker(robloxProcessId);
            _tracker.BoundsChanged += OnTrackerBounds;
            _tracker.VisibilityChanged += OnTrackerVisibility;
            _tracker.ForegroundChanged += OnTrackerForeground;
            _tracker.Closed += OnTrackerClosed;

            MacHotkeys.Pressed += OnMacHotkeyPressed;

            App.Logger.Info($"MacOverlayHost: created pid={robloxProcessId}");
        }

        public bool UsesSeparatePinnedWindows => true;

        public void Start()
        {
            _ = StartAsync();
        }

        public void WatchFriendsForPanel()
        {
            if (_friendsWatched || _disposed)
                return;

            _friendsWatched = true;
            Friends.Start();
        }

        private async Task StartAsync()
        {
            if (!await WaitForWindowAsync())
            {
                if (!_disposed)
                    App.Logger.Warn($"No window found for process {ProcessId}, not starting overlay");

                return;
            }

            if (_disposed)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed)
                    return;

                _window = new GameOverlay(this);

                _tracker.Screens = _window.Screens;

                _window.PrepareHidden();

                OverlaySearchBarService.Start(this);
                KeyOverlayService.Start(this);

                _tracker.Start();

                App.Logger.Info("MacOverlayHost: overlay created");
            });

            if (_disposed)
                return;

            SyncBounds();

            await Messaging.ConnectToUserhubAsync();
        }

        private async Task<bool> WaitForWindowAsync()
        {
            int elapsed = 0;

            while (!_disposed && elapsed < ResolveTimeoutMs)
            {
                if (_tracker.TryGetWindow(out _, out _))
                    return true;

                await Task.Delay(ResolvePollMs);
                elapsed += ResolvePollMs;
            }

            return false;
        }

        public void SyncBounds()
        {
            if (_disposed)
                return;

            if (Dispatcher.UIThread.CheckAccess())
                _tracker.Refresh();
            else
                Dispatcher.UIThread.Post(_tracker.Refresh);
        }

        public bool IsGameMinimised() => _tracker.IsMinimised;

        public bool IsGameForeground() => !_disposed && MacApp.FrontmostPid() == ProcessId;

        public void FocusGame() => MacApp.Activate(ProcessId);

        public IKeyOverlayInput? CreateKeyInput(SoftKeyProfile profile) =>
            new MacKeyOverlayInput(ProcessId, profile, () => _gameForeground);

        public void PrepareGameWindow(Window window) => PrepareOverlayWindow(window, MacWindow.WindowKind.Game);

        public void PrepareKeyOverlayWindow(Window window) => PrepareOverlayWindow(window, MacWindow.WindowKind.KeyOverlay);

        public void PrepareSearchBarWindow(Window window) => PrepareOverlayWindow(window, MacWindow.WindowKind.SearchBar);

        public void PreparePinnedPanelWindow(Window window) => PrepareOverlayWindow(window, MacWindow.WindowKind.PinnedPanel);

        private void PrepareOverlayWindow(Window window, MacWindow.WindowKind kind)
        {
            if (_disposed || _overlayWindows.ContainsKey(window))
                return;

            var state = new MacWindow(window, kind);

            if (!state.Attach())
            {
                state.Release();
                return;
            }

            window.Closed += OnOverlayWindowClosed;

            _overlayWindows[window] = state;
        }

        private void OnOverlayWindowClosed(object? sender, EventArgs e)
        {
            if (sender is Window window)
                ReleaseOverlayWindow(window);
        }

        public void ReleaseOverlayWindow(Window window)
        {
            window.Closed -= OnOverlayWindowClosed;

            if (_overlayWindows.Remove(window, out MacWindow? state))
                state.Release();
        }

        public void SetWindowCloaked(Window window, bool cloaked)
        {
            if (_overlayWindows.TryGetValue(window, out MacWindow? state))
                state.SetCloaked(cloaked);
        }

        public void SetClickThrough(Window window, bool clickThrough)
        {
            if (_overlayWindows.TryGetValue(window, out MacWindow? state))
                state.SetClickThrough(clickThrough);
        }

        public void SetOverlayRegion(Window window, IReadOnlyList<OverlayRegion> regions)
        {
            _ = window;
            _ = regions;
        }

        public void ClearOverlayRegion(Window window)
        {
            _ = window;
        }

        public void ClipOverlayToGame(Window window, OverlayRegion gameClip)
        {
            _ = window;
            _ = gameClip;
        }

        public bool IsWindowForeground(Window window) =>
            _overlayWindows.TryGetValue(window, out MacWindow? state) && state.IsForeground();

        public bool IsOwnProcessForeground() => MacApp.IsOwnProcessForeground();

        public void RaiseOverlayWindow(Window window)
        {
            if (_overlayWindows.TryGetValue(window, out MacWindow? state))
                state.Raise();
        }

        public bool RegisterHotkey(
            Window window,
            int id,
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description)
        {
            _ = window;

            return MacHotkeys.Register(
                id,
                modifiers,
                key,
                defaultModifiers,
                defaultKey,
                description);
        }

        public void UnregisterHotkey(Window window, int id)
        {
            _ = window;
            MacHotkeys.Unregister(id);
        }

        private void OnMacHotkeyPressed(int id) => HotkeyPressed?.Invoke(this, id);

        private void OnTrackerBounds(object? sender, OverlayBounds bounds)
        {
            OverlaySearchBarService.SetGameBounds(bounds);

            if (!_disposed)
                BoundsChanged?.Invoke(this, bounds);
        }

        private void OnTrackerVisibility(object? sender, bool visible)
        {
            if (_disposed)
                return;

            GameVisibilityChanged?.Invoke(this, visible);

            Dispatcher.UIThread.Post(() => _window?.RefreshPinned());
        }

        private void OnTrackerForeground(object? sender, int processId)
        {
            _gameForeground = processId == ProcessId;

            if (!_disposed)
                Dispatcher.UIThread.Post(() => _window?.Reanchor());
        }

        private void OnTrackerClosed(object? sender, EventArgs e)
        {
            if (!_disposed)
                WindowClosed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            OverlaySearchBarService.Stop();
            KeyOverlayService.Stop();

            MacHotkeys.Pressed -= OnMacHotkeyPressed;

            _tracker.BoundsChanged -= OnTrackerBounds;
            _tracker.VisibilityChanged -= OnTrackerVisibility;
            _tracker.ForegroundChanged -= OnTrackerForeground;
            _tracker.Closed -= OnTrackerClosed;
            _tracker.Dispose();

            Dispatcher.UIThread.Post(() =>
            {
                foreach (MacWindow state in _overlayWindows.Values)
                    state.Release();

                _overlayWindows.Clear();

                _window?.Close();
                _window = null;
            });

            Friends.Dispose();

            _ = Messaging.DisposeAsync().AsTask();
            GC.SuppressFinalize(this);
        }
    }
}
