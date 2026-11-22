// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

using Froststrap.Backend;
using Froststrap.Integrations.Overlay;
using Froststrap.Integrations.Overlay.Platform.Linux;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.Overlay;
using Froststrap.UI.Elements.Overlay;

using System.Linq;
using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform
{
    internal sealed class LinuxOverlayHost : IOverlayHost
    {
        private const int ResolveTimeoutMs = 30_000;
        private const int ResolvePollMs = 250;
        private const int MaxEvents = 64;
        private const int DeadTicks = 90;
        private const int HiddenFrameMs = 250;

        private static readonly int EventSize = Marshal.SizeOf<LinuxOverlay.InputEvent>();
        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

        public event EventHandler<OverlayBounds>? BoundsChanged;
        public event EventHandler<bool>? GameVisibilityChanged;
        public event EventHandler? WindowClosed;
        public event EventHandler<int>? HotkeyPressed;

        public RealtimeMessaging Messaging { get; } = new();
        public ActivityWatcher? ActivityWatcher { get; }
        public FriendPresence Friends { get; }
        public int ProcessId { get; }

        private readonly Dictionary<Window, LinuxOverlaySurface> _surfaces = [];
        private readonly Dictionary<int, LinuxOverlaySurface> _byId = [];
        private readonly HashSet<Window> _visibleWindows = [];
        private readonly Dictionary<int, DateTime> _lastFrame = [];
        private readonly HashSet<Window> _hiddenByGame = [];
        private readonly IntPtr _eventBuffer = Marshal.AllocHGlobal(EventSize * MaxEvents);

        private DispatcherTimer? _timer;

        private GameOverlay? _window;
        private LinuxOverlay.GameState _game;
        private OverlayBounds? _lastBounds;
        private Window? _focused;

        private bool _initialised;
        private bool _gameSeen;
        private bool _closedRaised;
        private bool _visibilityKnown;
        private bool _visible;
        private bool _friendsWatched;
        private bool _disposed;
        private int _missingTicks;

        public LinuxOverlayHost(int robloxProcessId, ActivityWatcher? activityWatcher)
        {
            ProcessId = robloxProcessId;
            ActivityWatcher = activityWatcher;
            Friends = new FriendPresence(activityWatcher, Messaging);

            try
            {
                _initialised = LinuxOverlay.Init();
            }
            catch (Exception ex)
            {
                App.Logger.Error("LinuxOverlayHost: init threw");
                App.Logger.Error(ex);
            }

            App.Logger.Info($"LinuxOverlayHost: created pid={robloxProcessId} initialised={_initialised} portal={LinuxHotkeys.PortalSupported}");
        }

        public bool UsesRenderedSurfaces => true;

        public bool UsesSeparatePinnedWindows => false;

        public void Start() => Dispatcher.UIThread.Post(() => _ = StartAsync());

        public void WatchFriendsForPanel()
        {
            if (_friendsWatched || _disposed)
                return;

            _friendsWatched = true;
            Friends.Start();
        }

        private async Task StartAsync()
        {
            if (!_initialised || _disposed)
                return;

            if (!await WaitForGameAsync())
            {
                if (!_disposed)
                    App.Logger.Warn($"No game found for process {ProcessId}, not starting the overlay");

                return;
            }

            if (_disposed)
                return;

            _timer = new DispatcherTimer(FrameInterval, DispatcherPriority.Input, (_, _) => Tick());

            _window = new GameOverlay(this);
            _window.PrepareHidden();
            _ = _window.PrepareForRenderingAsync();

            OverlaySearchBarService.Start(this);
            KeyOverlayService.Start(this);

            _timer.Start();

            SyncBounds();
            Dispatcher.UIThread.Post(SyncBounds, DispatcherPriority.Background);
            Dispatcher.UIThread.Post(SyncBounds, DispatcherPriority.Loaded);

            await Messaging.ConnectToUserhubAsync();
        }

        private async Task<bool> WaitForGameAsync()
        {
            int elapsed = 0;

            while (!_disposed && elapsed < ResolveTimeoutMs)
            {
                if (LinuxOverlay.GetGameState(out LinuxOverlay.GameState state)
                    && (state.Flags & LinuxOverlay.GamePresent) != 0)
                {
                    _game = state;
                    _gameSeen = true;
                    return true;
                }

                await Task.Delay(ResolvePollMs);
                elapsed += ResolvePollMs;
            }

            return false;
        }

        private void Tick()
        {
            if (_disposed || !_initialised)
                return;

            try { PollGame(); }
            catch (Exception ex) { App.Logger.Error("Tick: PollGame failed"); App.Logger.Error(ex); }

            try { PollHotkeys(); }
            catch (Exception ex) { App.Logger.Error("Tick: PollHotkeys failed"); App.Logger.Error(ex); }

            try { PollInput(); }
            catch (Exception ex) { App.Logger.Error("Tick: PollInput failed"); App.Logger.Error(ex); }

            try { RenderSurfaces(); }
            catch (Exception ex) { LogRenderFailure(ex); }
        }

        private DateTime _lastRenderFailureLog = DateTime.MinValue;

        private void LogRenderFailure(Exception ex)
        {
            if (DateTime.UtcNow - _lastRenderFailureLog < TimeSpan.FromSeconds(5))
                return;

            _lastRenderFailureLog = DateTime.UtcNow;
            App.Logger.Error("Tick: RenderSurfaces failed");
            App.Logger.Error(ex);
        }

        private void PollGame()
        {
            if (!LinuxOverlay.GetGameState(out LinuxOverlay.GameState state))
                return;

            _game = state;

            if ((state.Flags & LinuxOverlay.GamePresent) == 0)
            {
                if (_gameSeen && !_closedRaised)
                {
                    _missingTicks++;

                    if (_missingTicks > DeadTicks)
                    {
                        _closedRaised = true;
                        App.Logger.Info("Game went away");
                        WindowClosed?.Invoke(this, EventArgs.Empty);
                    }
                }

                return;
            }

            _missingTicks = 0;

            bool visible = (state.Flags & LinuxOverlay.GameVisible) != 0;

            if (!_visibilityKnown || visible != _visible)
            {
                _visibilityKnown = true;
                _visible = visible;
                App.Logger.Info($"LinuxOverlayHost: game visibility changed visible={visible}");
                RestoreRenderedWindows(visible);
                GameVisibilityChanged?.Invoke(this, visible);
                Dispatcher.UIThread.Post(() => _window?.RefreshPinned());
            }

            if (state.Width <= 0 || state.Height <= 0)
                return;

            var bounds = new OverlayBounds(
                new Rect(state.X, state.Y, state.Width, state.Height),
                (state.Flags & LinuxOverlay.GameMaximised) != 0);

            if (_lastBounds == bounds)
                return;

            Publish(bounds);
        }

        public void SyncBounds()
        {
            if (_disposed)
                return;

            if (!LinuxOverlay.GetGameState(out LinuxOverlay.GameState state))
                return;

            _game = state;

            if (state.Width <= 0 || state.Height <= 0)
                return;

            Publish(new OverlayBounds(
                new Rect(state.X, state.Y, state.Width, state.Height),
                (state.Flags & LinuxOverlay.GameMaximised) != 0));
        }

        private void Publish(OverlayBounds bounds)
        {
            _lastBounds = bounds;

            OverlaySearchBarService.SetGameBounds(bounds);
            BoundsChanged?.Invoke(this, bounds);
        }

        private void PollHotkeys()
        {
            for (int i = 0; i < 16; i++)
            {
                int id = LinuxOverlay.PollHotkey();

                if (id < 0)
                    return;

                App.Logger.Info($"LinuxOverlayHost: hotkey fired id={id}");
                HotkeyPressed?.Invoke(this, id);
            }
        }

        private void PollInput()
        {
            int count = LinuxOverlay.PollInput(_eventBuffer, MaxEvents);

            for (int i = 0; i < count; i++)
            {
                var e = Marshal.PtrToStructure<LinuxOverlay.InputEvent>(_eventBuffer + (i * EventSize));

                if (_byId.TryGetValue(e.Surface, out LinuxOverlaySurface? surface))
                    surface.Handle(e);
            }
        }

        private void RenderSurfaces()
        {
            if (_surfaces.Count == 0)
                return;

            var now = DateTime.UtcNow;

            foreach (LinuxOverlaySurface surface in _surfaces.Values)
            {
                bool visible = _visibleWindows.Contains(surface.Window);

                if (!visible)
                {
                    if (_lastFrame.TryGetValue(surface.Id, out DateTime previous)
                        && (now - previous).TotalMilliseconds < HiddenFrameMs)
                    {
                        continue;
                    }
                }

                _lastFrame[surface.Id] = now;

                (int width, int height) = SurfaceSize(surface);

                if (width <= 0 || height <= 0)
                    continue;

                surface.Render(width, height);
            }
        }

        private (int Width, int Height) SurfaceSize(LinuxOverlaySurface surface)
        {
            Window window = surface.Window;

            if (surface.Tracked)
                return ((int)Math.Ceiling(window.Width), (int)Math.Ceiling(window.Height));

            Size measured = MeasureRendered(window);
            int width = (int)Math.Ceiling(measured.Width);
            int height = (int)Math.Ceiling(measured.Height);

            if (width > 0 && height > 0)
                LinuxOverlay.SetRect(surface.Id, window.Position.X, window.Position.Y, width, height);

            return (width, height);
        }

        private void OnSurfaceFocus(LinuxOverlaySurface surface)
        {
            _focused = surface.Window;

            foreach (LinuxOverlaySurface other in _surfaces.Values)
                LinuxOverlay.SetKeyboard(other.Id, other.Id == surface.Id ? 1 : 0);
        }

        public bool IsRenderedVisible(Window window) => _visibleWindows.Contains(window);

        public void ShowRendered(Window window)
        {
            if (!_surfaces.TryGetValue(window, out LinuxOverlaySurface? surface))
                return;

            _visibleWindows.Add(window);
            App.Logger.Info($"LinuxOverlayHost: ShowRendered surface={surface.Id}");
            LinuxOverlay.SetVisible(surface.Id, true);
        }

        public void HideRendered(Window window)
        {
            if (!_surfaces.TryGetValue(window, out LinuxOverlaySurface? surface))
                return;

            _visibleWindows.Remove(window);
            App.Logger.Info($"LinuxOverlayHost: HideRendered surface={surface.Id}");
            LinuxOverlay.SetVisible(surface.Id, false);

            if (ReferenceEquals(_focused, window))
            {
                _focused = null;
                LinuxOverlay.SetKeyboard(surface.Id, 0);
            }
        }

        private void RestoreRenderedWindows(bool visible)
        {
            if (!visible)
            {
                _hiddenByGame.Clear();

                foreach (Window window in _visibleWindows.ToList())
                {
                    _hiddenByGame.Add(window);
                    HideRendered(window);
                }

                return;
            }

            foreach (Window window in _hiddenByGame.ToList())
                ShowRendered(window);

            _hiddenByGame.Clear();
        }

        public void FocusRendered(Window window)
        {
            if (_surfaces.TryGetValue(window, out LinuxOverlaySurface? surface))
                OnSurfaceFocus(surface);
        }

        public Size MeasureRendered(Window window)
        {
            if (window.Content is Control content)
            {
                content.Measure(Size.Infinity);
                Size desired = content.DesiredSize;

                if (double.IsFinite(desired.Width) && double.IsFinite(desired.Height)
                    && desired.Width > 0 && desired.Height > 0)
                {
                    return desired;
                }
            }

            double fallbackWidth = double.IsFinite(window.Width) && window.Width > 0 ? window.Width : 1;
            double fallbackHeight = double.IsFinite(window.Height) && window.Height > 0 ? window.Height : 1;

            return new Size(fallbackWidth, fallbackHeight);
        }

        public void PrepareGameWindow(Window window) => PrepareOverlayWindow(window, tracked: true, textInput: false);

        public void PrepareKeyOverlayWindow(Window window) => PrepareOverlayWindow(window, tracked: false, textInput: false);

        public void PrepareSearchBarWindow(Window window) => PrepareOverlayWindow(window, tracked: true, textInput: true);

        public void PreparePinnedPanelWindow(Window window) { }

        private void PrepareOverlayWindow(Window window, bool tracked, bool textInput)
        {
            if (_disposed || _surfaces.ContainsKey(window))
                return;

            if (window.Content is not Control content)
            {
                App.Logger.Error("LinuxOverlayHost: overlay window has no control content");
                return;
            }

            int id = LinuxOverlay.CreateSurface(tracked);

            var surface = new LinuxOverlaySurface(id, window, content)
            {
                Tracked = tracked,
                PreferTextInput = textInput,
                FocusRequested = OnSurfaceFocus
            };

            _surfaces[window] = surface;
            _byId[id] = surface;

            LinuxOverlay.SetVisible(id, false);

            LinuxOverlay.SetRegions(id, tracked ? FullRegion : []);
        }

        public void ReleaseOverlayWindow(Window window)
        {
            if (!_surfaces.Remove(window, out LinuxOverlaySurface? surface))
                return;

            _byId.Remove(surface.Id);
            _visibleWindows.Remove(window);
            _lastFrame.Remove(surface.Id);

            if (ReferenceEquals(_focused, window))
                _focused = null;

            LinuxOverlay.DestroySurface(surface.Id);
            surface.Dispose();
        }

        public void SetWindowCloaked(Window window, bool cloaked) { }

        public void SetClickThrough(Window window, bool clickThrough)
        {
            if (!_surfaces.TryGetValue(window, out LinuxOverlaySurface? surface))
                return;

            LinuxOverlay.SetRegions(surface.Id, clickThrough ? [] : FullRegion);
        }

        public void SetOverlayRegion(Window window, IReadOnlyList<OverlayRegion> regions)
        {
            if (!_surfaces.TryGetValue(window, out LinuxOverlaySurface? surface))
                return;

            var rects = new int[regions.Count * 4];

            for (int i = 0; i < regions.Count; i++)
            {
                OverlayRegion region = regions[i];
                rects[i * 4] = region.Left;
                rects[(i * 4) + 1] = region.Top;
                rects[(i * 4) + 2] = region.Right - region.Left;
                rects[(i * 4) + 3] = region.Bottom - region.Top;
            }

            LinuxOverlay.SetRegions(surface.Id, rects);
        }

        public void ClearOverlayRegion(Window window)
        {
            if (_surfaces.TryGetValue(window, out LinuxOverlaySurface? surface))
                LinuxOverlay.SetRegions(surface.Id, FullRegion);
        }

        public void ClipOverlayToGame(Window window, OverlayRegion gameClip) { }

        public bool IsGameMinimised() => (_game.Flags & LinuxOverlay.GameMinimised) != 0;

        public bool IsGameForeground() => (_game.Flags & LinuxOverlay.GameForeground) != 0;

        public void FocusGame() => LinuxOverlay.FocusGame();

        public IKeyOverlayInput? CreateKeyInput(SoftKeyProfile profile) => new LinuxKeyOverlayInput(profile);

        public bool IsWindowForeground(Window window) => ReferenceEquals(_focused, window);

        public bool IsOwnProcessForeground() => _focused is not null;

        public void RaiseOverlayWindow(Window window) { }

        public bool RegisterHotkey(
            Window window,
            int id,
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description) =>
            LinuxHotkeys.Register(id, modifiers, key, defaultModifiers, defaultKey, description);

        public void UnregisterHotkey(Window window, int id) => LinuxHotkeys.Unregister(id);

        private static int[] FullRegion => [0, 0, 16384, 16384];

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _timer?.Stop();
            _timer = null;

            OverlaySearchBarService.Stop();
            KeyOverlayService.Stop();

            _window?.Dispose();
            _window = null;

            foreach (LinuxOverlaySurface surface in _surfaces.Values)
            {
                LinuxOverlay.DestroySurface(surface.Id);
                surface.Dispose();
            }

            _surfaces.Clear();
            _byId.Clear();
            _visibleWindows.Clear();

            if (_initialised)
                LinuxOverlay.Shutdown();

            Marshal.FreeHGlobal(_eventBuffer);

            Friends.Dispose();

            _ = Messaging.DisposeAsync().AsTask();
            GC.SuppressFinalize(this);
        }
    }
}