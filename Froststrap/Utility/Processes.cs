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

    public static void KillBackgroundUpdater()
    {
        using EventWaitHandle handle = new(false, EventResetMode.AutoReset, "Froststrap-BackgroundUpdaterKillEvent");
        handle.Set();
    }
}
