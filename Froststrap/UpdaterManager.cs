// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Velopack;
using Velopack.Sources;

namespace Froststrap;

internal sealed class UpdaterManager(bool includePrerelease = false)
{
    // Maybe will change to our own API backend at some point in the future
    private const string UpdateRepository = "https://github.com/Froststrap/packages";

    private readonly UpdateManager manager = new(
        new GithubSource(UpdateRepository, null, includePrerelease)
    );

    public bool IsInstalled => manager.IsInstalled;

    public UpdateInfo? LastUpdate { get; private set; }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        UpdateInfo? update = await manager.CheckForUpdatesAsync();

        LastUpdate = update;

        return update;
    }

    public async Task DownloadUpdatesAsync(
        UpdateInfo update,
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await manager.DownloadUpdatesAsync(
            update,
            progress,
            cancellationToken
        );
    }

    public void ApplyUpdatesAndRestart(UpdateInfo update)
    {
        manager.ApplyUpdatesAndRestart(update);
    }
}
