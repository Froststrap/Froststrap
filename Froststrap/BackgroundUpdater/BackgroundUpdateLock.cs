// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.BackgroundUpdater;

internal sealed class BackgroundUpdateLock : IDisposable
{
    private const string Name = "BackgroundUpdater";

    private static string StopRequestPath => Path.Combine(Paths.Base, "BackgroundUpdater.stop");

    private readonly InterProcessLock _lock;

    private BackgroundUpdateLock(InterProcessLock heldLock) => _lock = heldLock;

    public static bool IsHeld
    {
        get
        {
            using var probe = new InterProcessLock(Name, TimeSpan.Zero);
            return !probe.IsAcquired;
        }
    }

    public static BackgroundUpdateLock? TryAcquire(TimeSpan timeout)
    {
        var heldLock = new InterProcessLock(Name, timeout);

        if (!heldLock.IsAcquired)
        {
            heldLock.Dispose();
            return null;
        }

        ClearStopRequest();
        return new BackgroundUpdateLock(heldLock);
    }

    public static void RequestStop()
    {
        try
        {
            File.WriteAllBytes(StopRequestPath, []);
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to request the background update to stop");
        }
    }

    public static async Task<bool> WaitForStopRequestAsync(CancellationToken token)
    {
        try
        {
            while (!File.Exists(StopRequestPath))
                await Task.Delay(500, token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        ClearStopRequest();
        return true;
    }

    public static async Task<bool> WaitForReleaseAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (IsHeld)
        {
            if (DateTime.UtcNow >= deadline)
                return false;

            await Task.Delay(250);
        }

        return true;
    }

    private static void ClearStopRequest()
    {
        try
        {
            File.Delete(StopRequestPath);
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to clear the background update stop request");
        }
    }

    public void Dispose() => _lock.Dispose();
}
