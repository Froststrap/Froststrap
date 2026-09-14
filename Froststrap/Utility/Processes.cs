// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Utility;

static class Processes
{
    public static Process[] GetProcessesSafe()
    {
        try
        {
            return Process.GetProcesses();
        }
        catch (ArithmeticException ex) // thanks microsoft
        {
            App.Logger.Error($"Unable to fetch processes! {ex}");
            return []; // can we retry?
        }
    }

    public static bool IsRobloxRunning()
    {
        Process[] processes = GetProcessesSafe();
        string processName = Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName);

        if (OperatingSystem.IsLinux())
            return processes.Any(x => x.ProcessName == "sober");
        else
            return processes.Any(x => x.ProcessName == processName);
    }

    public static void KillSober()
    {
        Process[] processes = GetProcessesSafe();
        foreach (var p in processes)
        {
            if (p.ProcessName == "sober")
            {
                try { p.Kill(); p.WaitForExit(1000); } catch { }
            }
        }
    }

    public static void KillBackgroundUpdater()
    {
        using EventWaitHandle handle = new(false, EventResetMode.AutoReset, "Froststrap-BackgroundUpdaterKillEvent");
        handle.Set();
    }
}
