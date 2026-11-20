// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Contracts
{
    /// <summary>
    /// Where installs come from: deployment CDN, an external package manager, or a
    /// self-managed install. Must not touch dialogs or progress UI.
    /// </summary>
    internal interface IClientSource
    {
        UpdatePolicy Policy { get; }

        Task<ClientInspection> InspectAsync(
            IClientLayout layout,
            ClientResolveContext context,
            CancellationToken cancellationToken = default);

        Task<InstallResult> InstallAsync(
            IClientLayout layout,
            ClientResolveContext context,
            InstallRequest request,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
