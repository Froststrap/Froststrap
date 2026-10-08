// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Integrations.OverlayModules;

namespace Froststrap.Integrations.Overlay.Platform
{
    internal class UnsupportedOverlayHost : IOverlayHost
    {
        public event EventHandler<OverlayBounds>? BoundsChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<bool>? GameVisibilityChanged
        {
            add { }
            remove { }
        }

        public event EventHandler? WindowClosed
        {
            add { }
            remove { }
        }

        public RealtimeMessaging Messaging { get; } = new();
        public ActivityWatcher? ActivityWatcher { get; }
        public FriendPresence Friends { get; }

        public UnsupportedOverlayHost(int robloxProcessId, ActivityWatcher? activityWatcher)
        {
            ActivityWatcher = activityWatcher;
            Friends = new FriendPresence(activityWatcher);
        }

        public void Start() { }
        public void SyncBounds() { }
        public bool IsGameMinimised() => false;
        public bool IsGameForeground() => false;
        public void FocusGame() { }
        public void WatchFriendsForPanel() { }
        public void AnchorAboveGame(IntPtr overlayHandle) { }

        public void Dispose()
        {
            Friends.Dispose();
            _ = Messaging.DisposeAsync().AsTask();
            GC.SuppressFinalize(this);
        }
    }
}