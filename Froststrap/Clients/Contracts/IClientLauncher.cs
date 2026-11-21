// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Contracts;

/// <summary>
/// Starts a client and reports how it becomes ready. Join parsing, matchmaking, and watcher
/// policy stay in the orchestrator.
/// </summary>
internal interface IClientLauncher
{
    Task<LaunchResult> LaunchAsync(
        ClientInstall install,
        LaunchRequest request,
        CancellationToken cancellationToken = default);
}
