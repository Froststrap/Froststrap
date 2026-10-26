// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;

using Froststrap.Enums;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay.Platform
{
    internal sealed class PlatformOverlayHost : IOverlayHost
    {
        private readonly IOverlayHost _host;

        public PlatformOverlayHost(int robloxProcessId, ActivityWatcher? activityWatcher)
        {
            _host = OperatingSystem.IsWindows() switch
            {
                true => new WindowsOverlayHost(robloxProcessId, activityWatcher),
                false when OperatingSystem.IsLinux() => new LinuxOverlayHost(robloxProcessId, activityWatcher),
                false when OperatingSystem.IsMacOS() => new MacOverlayHost(robloxProcessId, activityWatcher),
                _ => new UnsupportedOverlayHost(robloxProcessId, activityWatcher)
            };
        }

        public event EventHandler<OverlayBounds>? BoundsChanged
        {
            add => _host.BoundsChanged += value;
            remove => _host.BoundsChanged -= value;
        }

        public event EventHandler<bool>? GameVisibilityChanged
        {
            add => _host.GameVisibilityChanged += value;
            remove => _host.GameVisibilityChanged -= value;
        }

        public event EventHandler? WindowClosed
        {
            add => _host.WindowClosed += value;
            remove => _host.WindowClosed -= value;
        }

        public event EventHandler<int>? HotkeyPressed
        {
            add => _host.HotkeyPressed += value;
            remove => _host.HotkeyPressed -= value;
        }

        public RealtimeMessaging Messaging => _host.Messaging;
        public ActivityWatcher? ActivityWatcher => _host.ActivityWatcher;
        public FriendPresence Friends => _host.Friends;
        public int ProcessId => _host.ProcessId;

        public void Start() => _host.Start();
        public void SyncBounds() => _host.SyncBounds();
        public bool IsGameMinimised() => _host.IsGameMinimised();
        public bool IsGameForeground() => _host.IsGameForeground();
        public void FocusGame() => _host.FocusGame();
        public void WatchFriendsForPanel() => _host.WatchFriendsForPanel();

        public IKeyOverlayInput? CreateKeyInput(SoftKeyProfile profile) => _host.CreateKeyInput(profile);

        public bool UsesSeparatePinnedWindows => _host.UsesSeparatePinnedWindows;

        public void PrepareGameWindow(Window window) => _host.PrepareGameWindow(window);
        public void PrepareKeyOverlayWindow(Window window) => _host.PrepareKeyOverlayWindow(window);
        public void PrepareSearchBarWindow(Window window) => _host.PrepareSearchBarWindow(window);
        public void PreparePinnedPanelWindow(Window window) => _host.PreparePinnedPanelWindow(window);
        public void ReleaseOverlayWindow(Window window) => _host.ReleaseOverlayWindow(window);

        public void SetWindowCloaked(Window window, bool cloaked) => _host.SetWindowCloaked(window, cloaked);
        public void SetClickThrough(Window window, bool clickThrough) => _host.SetClickThrough(window, clickThrough);
        public void SetOverlayRegion(Window window, IReadOnlyList<OverlayRegion> regions) => _host.SetOverlayRegion(window, regions);
        public void ClearOverlayRegion(Window window) => _host.ClearOverlayRegion(window);
        public void ClipOverlayToGame(Window window, OverlayRegion gameClip) => _host.ClipOverlayToGame(window, gameClip);

        public bool IsWindowForeground(Window window) => _host.IsWindowForeground(window);
        public bool IsOwnProcessForeground() => _host.IsOwnProcessForeground();
        public void RaiseOverlayWindow(Window window) => _host.RaiseOverlayWindow(window);

        public bool CanDetectFocus => _host.CanDetectFocus;

        public bool RegisterHotkey(
            Window window,
            int id,
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description) =>
            _host.RegisterHotkey(window, id, modifiers, key, defaultModifiers, defaultKey, description);

        public void UnregisterHotkey(Window window, int id) => _host.UnregisterHotkey(window, id);

        public void Dispose() => _host.Dispose();
    }
}