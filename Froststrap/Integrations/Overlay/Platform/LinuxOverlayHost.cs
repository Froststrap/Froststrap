// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0
﻿using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

using Froststrap.Enums;
using Froststrap.Integrations.Overlay.Platform.Linux;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.Overlay;
using Froststrap.UI.Elements.Overlay;

namespace Froststrap.Integrations.Overlay.Platform
{
    internal sealed class LinuxOverlayHost : IOverlayHost
    {
        private const int MaxEvents = 64;
        private const int MissingTicksBeforeClosed = 5;

        private static readonly int EventSize = Marshal.SizeOf<LinuxOverlay.InputEvent>();

        public event EventHandler<OverlayBounds>? BoundsChanged;
        public event EventHandler<bool>? GameVisibilityChanged;
        public event EventHandler? WindowClosed;
        public event EventHandler<int>? HotkeyPressed;

        public RealtimeMessaging Messaging { get; } = new();
        public ActivityWatcher? ActivityWatcher { get; }
        public FriendPresence Friends { get; }
        public int ProcessId { get; }

        private readonly Dictionary<Window, LinuxWindow> _overlayWindows = [];
        private readonly Dictionary<int, LinuxOverlayRenderer> _renderers = [];

        private GameOverlay? _window;
        private LinuxKeyOverlayInput? _keyInput;

        private IntPtr _eventBuffer;
        private DispatcherTimer? _inputPump;
        private DispatcherTimer? _boundsTimer;

        private OverlayBounds? _lastBounds;
        private bool _lastMinimised = true;
        private int _missingTicks;
        private bool _windowClosedFired;
        private bool _friendsWatched;
        private bool _disposed;

        public LinuxOverlayHost(int robloxProcessId, ActivityWatcher? activityWatcher)
        {
            ProcessId = robloxProcessId;
            ActivityWatcher = activityWatcher;
            Friends = new FriendPresence(activityWatcher, Messaging);

            App.Logger.Info($"LinuxOverlayHost: created pid={robloxProcessId}");
        }

        public bool UsesSeparatePinnedWindows => false;

        public void Start() => _ = StartAsync();

        public void WatchFriendsForPanel()
        {
            if (_friendsWatched || _disposed)
                return;

            _friendsWatched = true;
            Friends.Start();
        }

        private async Task StartAsync()
        {
            if (_disposed)
                return;

            if (!LinuxOverlay.Init(ProcessId))
            {
                App.Logger.Warn("LinuxOverlayHost: init failed, overlay disabled");
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed)
                    return;

                _window = new GameOverlay(this);
                _window.PrepareHidden();

                OverlaySearchBarService.Start(this);
                KeyOverlayService.Start(this);

                _eventBuffer = Marshal.AllocHGlobal(EventSize * MaxEvents);

                _inputPump = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(8),
                    DispatcherPriority.Input,
                    (_, _) => PumpInput());
                _inputPump.Start();

                _boundsTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(100),
                    DispatcherPriority.Background,
                    (_, _) => PollBounds());
                _boundsTimer.Start();
            });

            if (_disposed)
                return;

            SyncBounds();
            await Messaging.ConnectToUserhubAsync();
        }

        internal void AttachRenderer(Window window, Control content, int surface)
        {
            if (_disposed || _renderers.ContainsKey(surface))
                return;

            var renderer = new LinuxOverlayRenderer(window, content, surface);
            _renderers[surface] = renderer;
            renderer.Start();
        }

        private void PumpInput()
        {
            if (_disposed || _eventBuffer == IntPtr.Zero)
                return;

            int count = LinuxOverlay.PollInput(_eventBuffer, MaxEvents);

            if (count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                IntPtr ptr = IntPtr.Add(_eventBuffer, i * EventSize);
                var e = Marshal.PtrToStructure<LinuxOverlay.InputEvent>(ptr);

                switch (e.Kind)
                {
                    case LinuxOverlay.KindHotkey:
                        HotkeyPressed?.Invoke(this, (int)e.Code);
                        break;
                    case LinuxOverlay.KindForeground:
                        PollBounds();
                        break;
                    case LinuxOverlay.KindKeyDown:
                    case LinuxOverlay.KindKeyUp:
                    case LinuxOverlay.KindButtonDown:
                    case LinuxOverlay.KindButtonUp:
                        _keyInput?.Handle(e);
                        foreach (var renderer in _renderers.Values)
                            renderer.HandleKey(e);
                        break;
                    case LinuxOverlay.KindText:
                        foreach (var renderer in _renderers.Values)
                            renderer.HandleText(e);
                        break;
                }
            }
        }

        private void PollBounds()
        {
            if (_disposed)
                return;

            bool minimised = LinuxOverlay.IsMinimised();

            if (minimised != _lastMinimised)
            {
                _lastMinimised = minimised;
                GameVisibilityChanged?.Invoke(this, !minimised);
            }

            if (LinuxOverlay.TryGetGameRect(out var rect) && rect.Valid != 0)
            {
                _missingTicks = 0;
                _windowClosedFired = false;

                var bounds = new OverlayBounds(
                    new Rect(rect.X, rect.Y, rect.Width, rect.Height),
                    false);

                if (bounds != _lastBounds)
                {
                    _lastBounds = bounds;
                    OverlaySearchBarService.SetGameBounds(bounds);
                    BoundsChanged?.Invoke(this, bounds);
                }
            }
            else
            {
                _missingTicks++;

                if (_missingTicks >= MissingTicksBeforeClosed && !_windowClosedFired)
                {
                    _windowClosedFired = true;
                    WindowClosed?.Invoke(this, EventArgs.Empty);
                }
            }

            if (!LinuxOverlay.UsesNativeWindow)
            {
                foreach (var (window, state) in _overlayWindows)
                {
                    if (window.IsVisible)
                        state.SyncGeometry(window.Position, window.Bounds.Size);
                }
            }
        }

        public void SyncBounds()
        {
            if (_disposed)
                return;

            if (Dispatcher.UIThread.CheckAccess())
                PollBounds();
            else
                Dispatcher.UIThread.Post(PollBounds);
        }

        public bool IsGameMinimised() => LinuxOverlay.IsMinimised();
        public bool IsGameForeground() => LinuxOverlay.IsForeground(ProcessId);
        public void FocusGame() => LinuxOverlay.FocusGame(ProcessId);

        public IKeyOverlayInput? CreateKeyInput(SoftKeyProfile profile)
        {
            if (_disposed)
                return null;

            if (_keyInput is not null)
                return _keyInput;

            _keyInput = new LinuxKeyOverlayInput(ProcessId, profile);
            LinuxOverlay.SetKeyMonitoring(true);

            return _keyInput;
        }

        public void PrepareGameWindow(Window window) => PrepareOverlayWindow(window, LinuxWindow.WindowKind.Game);
        public void PrepareKeyOverlayWindow(Window window) => PrepareOverlayWindow(window, LinuxWindow.WindowKind.KeyOverlay);
        public void PrepareSearchBarWindow(Window window) => PrepareOverlayWindow(window, LinuxWindow.WindowKind.SearchBar);
        public void PreparePinnedPanelWindow(Window window) { }

        private void PrepareOverlayWindow(Window window, LinuxWindow.WindowKind kind)
        {
            if (_disposed || _overlayWindows.ContainsKey(window))
                return;

            var state = new LinuxWindow(window, kind);

            if (!state.Attach())
            {
                state.Release();
                return;
            }

            _overlayWindows[window] = state;

            if (!LinuxOverlay.UsesNativeWindow && window.Content is Control content)
                AttachRenderer(window, content, state.Surface);
        }

        public void ReleaseOverlayWindow(Window window)
        {
            if (!_overlayWindows.Remove(window, out LinuxWindow? state))
                return;

            if (_renderers.Remove(state.Surface, out LinuxOverlayRenderer? renderer))
                renderer.Dispose();

            state.Release();
        }

        public void SetWindowCloaked(Window window, bool cloaked)
        {
            if (_overlayWindows.TryGetValue(window, out LinuxWindow? state))
                state.SetCloaked(cloaked);
        }

        public void SetClickThrough(Window window, bool clickThrough)
        {
            if (_overlayWindows.TryGetValue(window, out LinuxWindow? state))
                state.SetClickThrough(clickThrough);
        }

        public void SetOverlayRegion(Window window, IReadOnlyList<OverlayRegion> regions)
        {
            if (_overlayWindows.TryGetValue(window, out LinuxWindow? state))
                state.SetRegion(regions);
        }

        public void ClearOverlayRegion(Window window)
        {
            if (_overlayWindows.TryGetValue(window, out LinuxWindow? state))
                state.ClearRegion();
        }

        public void ClipOverlayToGame(Window window, OverlayRegion gameClip)
        {
            if (!_overlayWindows.TryGetValue(window, out LinuxWindow? state))
                return;

            state.SetRegion(
            [
                new OverlayRegion(
                    Math.Max(gameClip.Left, 0),
                    Math.Max(gameClip.Top, 0),
                    Math.Max(gameClip.Right, 0),
                    Math.Max(gameClip.Bottom, 0))
            ]);
        }

        public bool IsWindowForeground(Window window) =>
            _overlayWindows.TryGetValue(window, out LinuxWindow? state) && state.IsForeground();

        public bool IsOwnProcessForeground() => LinuxOverlay.IsForeground(Environment.ProcessId);

        public bool CanDetectFocus => LinuxOverlay.FocusKnown;

        public void RaiseOverlayWindow(Window window)
        {
            if (_overlayWindows.TryGetValue(window, out LinuxWindow? state))
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
            return LinuxHotkeys.Register(id, modifiers, key, defaultModifiers, defaultKey, description);
        }

        public void UnregisterHotkey(Window window, int id)
        {
            _ = window;
            LinuxHotkeys.Unregister(id);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            OverlaySearchBarService.Stop();
            KeyOverlayService.Stop();

            _inputPump?.Stop();
            _inputPump = null;

            _boundsTimer?.Stop();
            _boundsTimer = null;

            LinuxOverlay.SetKeyMonitoring(false);

            _keyInput?.Dispose();
            _keyInput = null;

            foreach (var renderer in _renderers.Values)
                renderer.Dispose();

            _renderers.Clear();

            if (_eventBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_eventBuffer);
                _eventBuffer = IntPtr.Zero;
            }

            foreach (var state in _overlayWindows.Values)
                state.Release();

            _overlayWindows.Clear();

            Dispatcher.UIThread.Post(() =>
            {
                _window?.Close();
                _window = null;
            });

            Friends.Dispose();
            _ = Messaging.DisposeAsync().AsTask();

            LinuxOverlay.Shutdown();

            GC.SuppressFinalize(this);
        }
    }
}