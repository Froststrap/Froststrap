// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Integrations.OverlayModules;

namespace Froststrap.Integrations.Overlay
{
    internal interface IOverlayHost : IDisposable
    {
        event EventHandler<OverlayBounds>? BoundsChanged;
        event EventHandler<bool>? GameVisibilityChanged;
        event EventHandler? WindowClosed;

        RealtimeMessaging Messaging { get; }
        ActivityWatcher? ActivityWatcher { get; }
        FriendPresence Friends { get; }

        void Start();
        void SyncBounds();
        bool ShowToast(string title, string message);
        void DismissToast();
        bool IsGameMinimised();
        bool IsGameForeground();
        void FocusGame();
        void WatchFriendsForPanel();
        void AnchorAboveGame(IntPtr overlayHandle);
    }
}
