// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Registry;

/// <summary>
/// Registration entry for one client on one or more host platforms. Factories are invoked
/// once per target by <see cref="ClientRegistry"/>.
/// </summary>
internal sealed record ClientDescriptor
{
    public required string ClientId { get; init; }

    public required string DisplayName { get; init; }

    public required ClientTarget Target { get; init; }

    public required IReadOnlyList<HostPlatform> HostPlatforms { get; init; }

    public required Func<IRobloxClient> Factory { get; init; }

    /// <summary>
    /// Existing state file name, kept stable so no migration is needed when a client moves
    /// behind this seam.
    /// </summary>
    public string StateFileName { get; init; } = "";
}
