// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Contracts
{
    /// <summary>
    /// One way of running Roblox. Owns identity, state, and the inspect/install/launch entry
    /// points. Does not own UI, matchmaking, locking, or dialogs.
    /// </summary>
    internal interface IRobloxClient
    {
        string ClientId { get; }

        string DisplayName { get; }

        string LaunchLabel { get; }

        ClientTarget Target { get; }

        IClientLayout Layout { get; }

        ClientCapabilities Capabilities { get; }

        ILogLocator Logs { get; }

        IProcessService Processes { get; }

        IClientLauncher Launcher { get; }

        ICookieStore Cookies { get; }

        ISettingsSync? SettingsSync { get; }

        IChannelWriter Channel { get; }

        DistributionState State { get; }

        /// <summary>
        /// The install resolved from the current state. Pure path composition: no I/O and no
        /// settings reads inside the layout. Use <see cref="Inspect"/> to learn whether it
        /// exists.
        /// </summary>
        ClientInstall Install { get; }

        /// <summary>Synchronous install check, for UI that cannot await.</summary>
        ClientInspection Inspect();

        Task<ClientInspection> InspectAsync(CancellationToken cancellationToken = default);

        Task<InstallResult> EnsureInstalledAsync(
            InstallRequest request,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default);

        Task<LaunchResult> LaunchAsync(
            LaunchRequest request,
            CancellationToken cancellationToken = default);

        Task PrepareEnvironmentAsync();
    }
}
