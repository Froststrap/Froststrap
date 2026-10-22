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
    internal class UnsupportedOverlayHost : IOverlayHost
    {
        public event EventHandler<OverlayBounds>? BoundsChanged { add { } remove { } }
        public event EventHandler<bool>? GameVisibilityChanged { add { } remove { } }
        public event EventHandler? WindowClosed { add { } remove { } }
        public event EventHandler<int>? HotkeyPressed { add { } remove { } }

        public RealtimeMessaging Messaging { get; } = new();
        public ActivityWatcher? ActivityWatcher { get; }
        public FriendPresence Friends { get; }
        public int ProcessId { get; }

        public UnsupportedOverlayHost(int robloxProcessId, ActivityWatcher? activityWatcher)
        {
            ProcessId = robloxProcessId;
            ActivityWatcher = activityWatcher;
            Friends = new FriendPresence(activityWatcher, Messaging);
        }

        public void Start() { }
        public void SyncBounds() { }
        public bool IsGameMinimised() => false;
        public bool IsGameForeground() => false;
        public void FocusGame() { }
        public void WatchFriendsForPanel() { }

        public IKeyOverlayInput? CreateKeyInput(SoftKeyProfile profile) => null;

        public void PrepareGameWindow(Window window) { }
        public void PrepareKeyOverlayWindow(Window window) { }
        public void PrepareSearchBarWindow(Window window) { }
        public void ReleaseOverlayWindow(Window window) { }

        public void SetWindowCloaked(Window window, bool cloaked) { }
        public void SetClickThrough(Window window, bool clickThrough) { }
        public void SetOverlayRegion(Window window, IReadOnlyList<OverlayRegion> regions) { }
        public void ClearOverlayRegion(Window window) { }
        public void ClipOverlayToGame(Window window, OverlayRegion gameClip) { }

        public bool IsWindowForeground(Window window) => false;
        public bool IsOwnProcessForeground() => false;
        public void RaiseOverlayWindow(Window window) { }

        public bool RegisterHotkey(
            Window window,
            int id,
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description) => false;

        public void UnregisterHotkey(Window window, int id) { }

        public void Dispose()
        {
            Friends.Dispose();
            _ = Messaging.DisposeAsync().AsTask();
            GC.SuppressFinalize(this);
        }
    }
}