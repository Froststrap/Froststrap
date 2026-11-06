// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

// How to test the updater in a dev environment without it reaching out to GitHub.
//
// The updater only works in a Velopack-installed build (UpdateManager.IsInstalled),
// so `dotnet run` can't actually utilise it. To test:
//   1. Install the Velopack CLI via: dotnet tool install -g vpk
//   2. dotnet publish an older version with -p:AppVersion=<old> into publish/<old>
//   3. vpk pack it into Releases/old (--outputDir Releases/old). This is the build
//      you'll run, kept apart so it isn't overwritten by the newer version
//   4. dotnet publish a newer version into publish/<new> and vpk pack it into
//      Releases (--outputDir Releases). This is the update feed
//   5. Run the old installer/build with FROSTSTRAP_UPDATE_SOURCE set to the absolute
//      path of the Releases folder, e.g. in Command Prompt:
//
//      set FROSTSTRAP_UPDATE_SOURCE=C:\path\to\Releases
//      Releases\old\<old installer>.exe
//
//      Or in Bash/Zsh:
//
//      FROSTSTRAP_UPDATE_SOURCE="/path/to/Releases" ./Releases/old/<old installer/AppImage>
//
//   6. Click "Check for Updates" in Deployment settings
//
// Leave the variable unset for normal behaviour.

using Velopack;
using Velopack.Sources;

namespace Froststrap;

internal sealed class UpdaterManager(bool includePrerelease = false)
{
    // Maybe will change to our own API backend at some point in the future
    private const string UpdateRepository = "https://github.com/Froststrap/packages";

    private readonly UpdateManager manager = CreateManager(includePrerelease);

    private static UpdateManager CreateManager(bool includePrerelease)
    {
        // Dev testing only: point the updater at a local folder of vpk output instead of GitHub
        string? devSource = Environment.GetEnvironmentVariable("FROSTSTRAP_UPDATE_SOURCE");

        if (!string.IsNullOrWhiteSpace(devSource))
            return new UpdateManager(devSource);

        return new UpdateManager(
            new GithubSource(UpdateRepository, null, includePrerelease)
        );
    }

    public bool IsInstalled => manager.IsInstalled;

    // CI builds (e.g. 0.0.2-ci.106) must never update themselves, otherwise they jump straight to the latest release
    public static bool IsCiBuild => App.Version.Contains("-ci.", StringComparison.OrdinalIgnoreCase);

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
