// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;

namespace Froststrap.BackgroundUpdater;

internal static class BackgroundUpdate
{
    private const string ProcessMarker = "FROSTSTRAP_BACKGROUND_UPDATER";

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    public static bool IsCurrentProcess { get; } = ConsumeProcessMarker();

    public static bool IsRunning => BackgroundUpdateLock.IsHeld;

    public static bool TryDefer(bool isStudio, bool mustUpgrade, Version? latestVersion, IAppData appData)
    {
        if (IsCurrentProcess || isStudio || mustUpgrade)
            return false;

        if (IsRunning)
        {
            App.Logger.Info("A background update is already running, launching the installed version");
            return true;
        }

        if (!BackgroundUpdatePolicy.IsEligible(latestVersion, appData))
            return false;

        return Start();
    }

    public static async Task StopAsync()
    {
        if (IsCurrentProcess || !IsRunning)
            return;

        App.Logger.Info("Stopping the running background update");
        BackgroundUpdateLock.RequestStop();

        if (!await BackgroundUpdateLock.WaitForReleaseAsync(StopTimeout))
            App.Logger.Warn("The background update did not stop in time");
    }

    public static async Task RunAsync()
    {
        using var updateLock = BackgroundUpdateLock.TryAcquire(TimeSpan.FromSeconds(2));

        if (updateLock is null)
        {
            App.Logger.Info("Another background update is already running, exiting");
            App.Terminate();
            return;
        }

        App.LaunchSettings.NoLaunchFlag.Active = true;
        App.LaunchSettings.Silent = true;

        App.Logger.Info("Initializing bootstrapper");
        var bootstrapper = new Bootstrapper(LaunchMode.Player);
        App.Bootstrapper = bootstrapper;

        using var stopWatcher = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            if (!await BackgroundUpdateLock.WaitForStopRequestAsync(stopWatcher.Token))
                return;

            App.Logger.Info("Received a stop request, cancelling the background update");
            bootstrapper.Cancel();
        });

        try
        {
            await Task.Run(bootstrapper.Run);
            App.Logger.Info("Background update finished");
        }
        catch (Exception ex)
        {
            App.Logger.Error("An exception occurred during the background update");
            await App.FinalizeExceptionHandling(ex);
        }
        finally
        {
            await stopWatcher.CancelAsync();
        }

        App.Terminate();
    }

    private static bool Start()
    {
        App.Logger.Info("Starting background update");

        try
        {
            var startInfo = new ProcessStartInfo(Paths.Process) { UseShellExecute = false };
            startInfo.Environment[ProcessMarker] = "1";

            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to start the background update");
            return false;
        }
    }

    private static bool ConsumeProcessMarker()
    {
        bool isMarked = Environment.GetEnvironmentVariable(ProcessMarker) == "1";

        if (isMarked)
            Environment.SetEnvironmentVariable(ProcessMarker, null);

        return isMarked;
    }
}
