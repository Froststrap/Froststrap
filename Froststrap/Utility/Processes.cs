// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Utility;

static class Processes
{
    public static Process[] GetProcessList()
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

    public static void Close(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);

            App.Logger.Info($"Closing process '{process.ProcessName}' (pid={pid})");

            if (process.HasExited)
            {
                App.Logger.Info($"PID {pid} has already exited");
                return;
            }

            process.CloseMainWindow();
        }
        catch (Exception ex)
        {
            App.Logger.Error($"PID {pid} could not be closed");
            App.Logger.Error(ex);
        }
    }
}
