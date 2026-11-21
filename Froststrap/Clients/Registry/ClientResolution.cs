// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Registry;

/// <summary>
/// What was resolved for a target. Logged once at startup and surfaced in diagnostics.
/// </summary>
internal sealed record ClientResolution
{
    public required HostPlatform Host { get; init; }

    public required ClientTarget Target { get; init; }

    public required string ClientId { get; init; }

    public required string DisplayName { get; init; }

    public required ClientCapabilities Capabilities { get; init; }

    public string Summary => $"{DisplayName} ({ClientId}) on {Host}";

    public override string ToString()
        => $"{Target}={ClientId} ({DisplayName}) on {Host} [{Capabilities}]";
}
