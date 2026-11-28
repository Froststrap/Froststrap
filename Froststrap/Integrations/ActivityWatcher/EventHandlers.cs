// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Integrations.ActivityWatcher;

internal static class Events
{
    public static event EventHandler? OnHistoryUpdated;
    public static event EventHandler<string>? OnLogEntry;
    public static event EventHandler? OnGameJoining;
    public static event EventHandler? ShowNotif;
    public static event EventHandler? OnGameJoin;
    public static event EventHandler? OnGameLeave;
    public static event EventHandler? OnStudioPlaceOpened;
    public static event EventHandler? OnStudioPlaceClosed;
    public static event EventHandler? OnLogOpen;
    public static event EventHandler? OnAppClose;
    public static event EventHandler<Message>? OnRPCMessage;
    public static event EventHandler<StudioMessage>? OnStudioRPCMessage;

    internal static void RaiseHistoryUpdated(Watcher sender) => OnHistoryUpdated?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseLogEntry(Watcher sender, string entry) => OnLogEntry?.Invoke(sender, entry);
    internal static void RaiseGameJoining(Watcher sender) => OnGameJoining?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseShowNotif(Watcher sender) => ShowNotif?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseGameJoin(Watcher sender) => OnGameJoin?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseGameLeave(Watcher sender) => OnGameLeave?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseStudioPlaceOpened(Watcher sender) => OnStudioPlaceOpened?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseStudioPlaceClosed(Watcher sender) => OnStudioPlaceClosed?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseLogOpen(Watcher sender) => OnLogOpen?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseAppClose(Watcher sender) => OnAppClose?.Invoke(sender, EventArgs.Empty);
    internal static void RaiseRPCMessage(Watcher sender, Message message) => OnRPCMessage?.Invoke(sender, message);
    internal static void RaiseStudioRPCMessage(Watcher sender, StudioMessage message) => OnStudioRPCMessage?.Invoke(sender, message);
}
