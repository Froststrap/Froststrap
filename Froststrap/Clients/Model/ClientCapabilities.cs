// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Model;

/// <summary>
/// Declarative properties used to choose behavior. Callers ask what a client can do,
/// never what it is called.
/// </summary>
internal sealed record ClientCapabilities
{
    public bool RequiresDeploymentApi { get; init; }

    public ModSupport ModSupport { get; init; } = ModSupport.None;

    public RestoreStrategy RestoreStrategy { get; init; } = RestoreStrategy.None;

    public bool SupportsStudioLaunch { get; init; }

    public bool SupportsCustomIntegrations { get; init; }

    public static ClientCapabilities None { get; } = new();
}
