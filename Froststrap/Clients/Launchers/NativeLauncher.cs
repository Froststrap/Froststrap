// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Launchers;

internal sealed class NativeLauncher(IClientLayout layout) : IClientLauncher
{
    private const int ErrorCancelled = 1223;

    private static readonly TimeSpan BundlePidTimeout = TimeSpan.FromSeconds(5);

    public async Task<LaunchResult> LaunchAsync(
        ClientInstall install,
        LaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        string executablePath = install.ExecutablePath!;
        string commandLine = request.CommandLine ?? string.Empty;

        var startInfo = new ProcessStartInfo
        {
            FileName = install.IsBundle ? "open" : executablePath,
            Arguments = install.IsBundle ? BundleArguments(executablePath, commandLine, request.Mode) : commandLine,
            WorkingDirectory = install.InstallRoot,
            UseShellExecute = install.IsBundle
        };

        if (request.RunAsAdmin)
        {
            startInfo.Verb = "runas";
            startInfo.UseShellExecute = true;
        }
        else if (request.Mode == LaunchMode.StudioAuth)
        {
            Process.Start(startInfo);
            return new LaunchResult { Started = true };
        }

        int pid;

        // v2.2.0 - byfron will trip if we keep a process handle open for over a minute, so we're doing this now
        try
        {
            using var process = Process.Start(startInfo)!;

            pid = process.Id;

            if (install.IsBundle)
            {
                int bundlePid = await FindNewestProcessAsync(layout.ProcessNames[0], BundlePidTimeout);

                if (bundlePid == 0)
                {
                    App.Logger.Warn("Could not locate Roblox process, falling back to open PID.");
                }
                else
                {
                    pid = bundlePid;
                    App.Logger.Info($"Detected Roblox process PID: {pid}");
                }
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            throw new OperationCanceledException(ex.Message, ex);
        }
        catch (Exception)
        {
            // attempt a reinstall on next launch
            try
            {
                if (install.IsBundle)
                    Directory.Delete(executablePath, true);
                else
                    File.Delete(executablePath);
            }
            catch (Exception cleanupEx)
            {
                App.Logger.Warn($"Failed to remove client to force a reinstall: {cleanupEx.Message}");
            }

            throw;
        }

        return new LaunchResult
        {
            Started = true,
            Pid = pid
        };
    }

    private static string BundleArguments(string bundlePath, string commandLine, LaunchMode mode)
    {
        string launch = commandLine.Trim();

        if (launch.Length == 0 && mode == LaunchMode.Player)
            launch = "roblox-player:";

        if (launch.Length == 0)
            return $"-n \"{bundlePath}\"";

        if (launch.StartsWith('-'))
            return $"-n \"{bundlePath}\" --args {launch}";

        App.Logger.Info("Delivering the launch url to Roblox through Launch Services");
        string safeUrl = launch.Replace("\"", "%22", StringComparison.Ordinal);
        return $"-n -a \"{bundlePath}\" \"{safeUrl}\"";
    }

    private static async Task<int> FindNewestProcessAsync(string processName, TimeSpan timeout)
    {
        var startTime = DateTime.Now;

        while (DateTime.Now - startTime < timeout)
        {
            Process? target = null;
            DateTime latestStartTime = DateTime.MinValue;

            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (process.StartTime > latestStartTime)
                    {
                        latestStartTime = process.StartTime;
                        target = process;
                    }
                }
                catch (Exception)
                {
                    // process might have exited between enumeration and accessing StartTime
                }
            }

            if (target != null)
                return target.Id;

            await Task.Delay(100);
        }

        return 0;
    }
}
