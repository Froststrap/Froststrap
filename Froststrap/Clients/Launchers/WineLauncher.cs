// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Launchers;

internal sealed class WineLauncher(IClientLayout layout) : IClientLauncher
{
    public static string WineRoot => Path.Combine(Paths.Base, "Wine");

    public static string WineExecutable => Path.Combine(WineRoot, "kombucha", "bin", "wine");

    public Task<LaunchResult> LaunchAsync(
        ClientInstall install,
        LaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        var wine = new WineManager(WineRoot);
        string versionDirectory = request.WorkingDirectory ?? install.InstallRoot;

        var env = new Dictionary<string, string>
        {
            { "WINEDLLOVERRIDES", "d3d9,d3d10core,d3d11,dxgi=n,b" }
        };

        if (App.Settings.Prop.StudioRenderer == StudioRenderer.DXVK)
        {
            env["DXVK_LOG_LEVEL"] = "warn";
            env["DXVK_STATE_CACHE_PATH"] = Paths.Cache;
        }

        if (App.Settings.Prop.StudioDebug)
            env["WINEDEBUG"] = "warn+seh,fixme-all,err-kerberos,err-ntlm,err-combase";

        foreach (var userEnv in App.Settings.Prop.StudioEnvironmentVariables)
            env[userEnv.Key] = userEnv.Value;

        string baseCommand = $"\"{Path.Combine(versionDirectory, layout.ExecutableName!)}\" {request.CommandLine}";
        string finalCommand = baseCommand;

        string? virtualDesktop = App.Settings.Prop.StudioVirtualDesktop;
        if (!string.IsNullOrEmpty(virtualDesktop))
            finalCommand = $"explorer /desktop={Guid.NewGuid()},{virtualDesktop} {baseCommand}";

        string? customLauncher = App.Settings.Prop.StudioLauncher;
        if (!string.IsNullOrEmpty(customLauncher))
            finalCommand = customLauncher.Replace("%command%", finalCommand, StringComparison.Ordinal);

        var startInfo = new ProcessStartInfo
        {
            FileName = WineExecutable,
            Arguments = finalCommand,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = versionDirectory
        };

        startInfo.Environment["WINEPREFIX"] = wine.PrefixDir;
        foreach (var kv in env)
            startInfo.Environment[kv.Key] = kv.Value;

        int pid;

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                App.Logger.Error("Failed to start Roblox Studio process.");
                return Task.FromResult(new LaunchResult { Started = false });
            }

            pid = process.Id;
            App.Logger.Info($"Roblox Studio started with Wine (PID {pid})");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to launch Studio: {ex.Message}");
            return Task.FromResult(new LaunchResult { Started = false });
        }

        string logsDirectory = Path.Combine(wine.PrefixDir, "drive_c", "users", Environment.UserName, "AppData", "Local", "Roblox", "logs");
        Directory.CreateDirectory(logsDirectory);

        return Task.FromResult(new LaunchResult
        {
            Started = true,
            Pid = pid
        });
    }
}
