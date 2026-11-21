// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Contracts;

/// <summary>
/// One way of running Roblox. Owns identity, state, and the inspect/install/launch entry
/// points. Does not own UI, matchmaking, locking, or dialogs.
/// </summary>
internal interface IRobloxClient
{
    string ClientId { get; }

    string DisplayName { get; }

    string LaunchLabel { get; }

    string StartingStatus { get; }

    ClientTarget Target { get; }

    IClientLayout Layout { get; }

    ClientCapabilities Capabilities { get; }

    ILogLocator Logs { get; }

    IProcessService Processes { get; }

    ICookieStore Cookies { get; }

    ISettingsSync? SettingsSync { get; }

    IChannelWriter Channel { get; }

    IHostRegistration Registration { get; }

    IRuntimeEnvironment? RuntimeEnvironment { get; }

    DistributionState State { get; }

    /// <summary>
    /// The install resolved from the current state. Pure path composition: no I/O and no
    /// settings reads inside the layout. Use <see cref="IsInstalled"/> to learn whether it
    /// exists.
    /// </summary>
    ClientInstall Install { get; }

    bool IsInstalled();

    Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        CancellationToken cancellationToken = default);

    void Kill(int pid);

    Task CloseAsync(int pid, TimeSpan gracePeriod, CancellationToken cancellationToken = default);

    Task CloseAllAsync(KillRequest request, CancellationToken cancellationToken = default);

    Task CloseCrashHandlersAsync(CancellationToken cancellationToken = default);

    Task<InstallResult> EnsureInstalledAsync(IInstallReporter reporter);

    Task UpdateAsync(IInstallReporter reporter, CancellationToken cancellationToken = default);

    Task FinalizeInstallAsync(string versionDirectory);

    Task PrepareEnvironmentAsync();

    bool ResetInstalledPackages();

    string DescribeLaunchFailure(string details);
}
