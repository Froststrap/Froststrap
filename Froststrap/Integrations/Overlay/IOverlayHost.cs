// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;

using Froststrap.Enums;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay
{
    internal interface IOverlayHost : IDisposable
    {
        event EventHandler<OverlayBounds>? BoundsChanged;
        event EventHandler<bool>? GameVisibilityChanged;
        event EventHandler? WindowClosed;
        event EventHandler<int>? HotkeyPressed;

        RealtimeMessaging Messaging { get; }
        ActivityWatcher? ActivityWatcher { get; }
        FriendPresence Friends { get; }
        int ProcessId { get; }

        void Start();
        void SyncBounds();
        bool IsGameMinimised();
        bool IsGameForeground();
        void FocusGame();
        void WatchFriendsForPanel();

        IKeyOverlayInput? CreateKeyInput(SoftKeyProfile profile);

        bool UsesSeparatePinnedWindows { get; }

        void PrepareGameWindow(Window window);
        void PrepareKeyOverlayWindow(Window window);
        void PrepareSearchBarWindow(Window window);
        void PreparePinnedPanelWindow(Window window);
        void ReleaseOverlayWindow(Window window);

        void SetWindowCloaked(Window window, bool cloaked);
        void SetClickThrough(Window window, bool clickThrough);
        void SetOverlayRegion(Window window, IReadOnlyList<OverlayRegion> regions);
        void ClearOverlayRegion(Window window);
        void ClipOverlayToGame(Window window, OverlayRegion gameClip);

        bool IsWindowForeground(Window window);
        bool IsOwnProcessForeground();
        void RaiseOverlayWindow(Window window);

        bool CanDetectFocus => true;

        bool RegisterHotkey(
            Window window,
            int id,
            KeyModifiers modifiers,
            Key key,
            KeyModifiers defaultModifiers,
            Key defaultKey,
            string description);

        void UnregisterHotkey(Window window, int id);
    }
}