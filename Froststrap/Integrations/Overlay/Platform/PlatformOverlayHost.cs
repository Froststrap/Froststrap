// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Integrations.OverlayModules;

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

        public RealtimeMessaging Messaging => _host.Messaging;
        public ActivityWatcher? ActivityWatcher => _host.ActivityWatcher;
        public FriendPresence Friends => _host.Friends;

        public void Start() => _host.Start();
        public void SyncBounds() => _host.SyncBounds();
        public bool IsGameMinimised() => _host.IsGameMinimised();
        public bool IsGameForeground() => _host.IsGameForeground();
        public void FocusGame() => _host.FocusGame();
        public void WatchFriendsForPanel() => _host.WatchFriendsForPanel();
        public void AnchorAboveGame(IntPtr overlayHandle) => _host.AnchorAboveGame(overlayHandle);
        public void Dispose() => _host.Dispose();
    }
}