// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Model;

/// <summary>
/// Host values a layout needs in order to produce absolute paths. Passed in explicitly so
/// layouts stay pure and can be resolved for a different host in tests or diagnostics.
/// </summary>
internal sealed record ClientResolveContext
{
    public required string VersionsRoot { get; init; }

    public required string UserProfile { get; init; }

    public required string LocalAppData { get; init; }

    public required string TempRoot { get; init; }

    public string DataRoot { get; init; } = "";

    public string UserName { get; init; } = "";

    /// <summary>
    /// Mirrors the user's "static directory" setting. Passed in rather than read from
    /// settings so a layout never reaches into <c>App.Settings</c>.
    /// </summary>
    public bool StaticDirectory { get; init; }

    public DistributionState State { get; init; } = new();
}
