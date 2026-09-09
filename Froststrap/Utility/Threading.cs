// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Froststrap.Utility;

internal class Threading
{
    public static async Task RunAsync(string cmd, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(cmd, args) { UseShellExecute = false, CreateNoWindow = true });
        await p!.WaitForExitAsync();
    }

    public static void ShellExecute(string path, bool select = false)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = select ? "explorer.exe" : path,
                    UseShellExecute = true
                };

                if (select)
                {
                    psi.ArgumentList.Add($"/select,\"{path}\"");
                }

                Process.Start(psi);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string target = select ? (Path.GetDirectoryName(path) ?? path) : path;

                var psi = new ProcessStartInfo("xdg-open")
                {
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(target);

                Process.Start(psi);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var psi = new ProcessStartInfo("open")
                {
                    UseShellExecute = false
                };

                if (select)
                {
                    psi.ArgumentList.Add("-R");
                }
                psi.ArgumentList.Add(path);

                Process.Start(psi);
            }
        }
        catch (Win32Exception ex)
        {
            if (ex.NativeErrorCode != (int)ErrorCode.CO_E_APPNOTFOUND)
                throw;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32,OpenAs_RunDLL {path}",
                    UseShellExecute = true
                });
            }
        }
    }
}
