// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Integrations.ActivityWatcher;

internal sealed class Events
{
    public event EventHandler? OnHistoryUpdated;
    public event EventHandler<string>? OnLogEntry;
    public event EventHandler? ShowNotif;
    public event EventHandler? OnGameJoin;
    public event EventHandler? OnGameLeave;
    public event EventHandler? OnStudioPlaceOpened;
    public event EventHandler? OnStudioPlaceClosed;
    public event EventHandler? OnLogOpen;
    public event EventHandler? OnAppClose;
    public event EventHandler<Message>? OnRPCMessage;
    public event EventHandler<StudioMessage>? OnStudioRPCMessage;

    public void RaiseOnRPCMessage(Message message) => OnRPCMessage?.Invoke(this, message);
    public void RaiseOnStudioRPCMessage(StudioMessage message) => OnStudioRPCMessage?.Invoke(this, message);
}