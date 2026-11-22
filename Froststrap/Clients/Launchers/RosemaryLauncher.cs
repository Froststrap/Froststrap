// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Launchers;

internal sealed class RosemaryLauncher(Func<string?> rosemaryPath) : IClientLauncher
{
    public Task<LaunchResult> LaunchAsync(
        ClientInstall install,
        LaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        string rosemary = rosemaryPath() ?? throw new FileNotFoundException("Rosemary is not installed");

        var startInfo = new ProcessStartInfo
        {
            FileName = rosemary,
            WorkingDirectory = install.InstallRoot,
            UseShellExecute = false
        };

        foreach (var variable in App.Settings.Prop.RosemaryEnvironmentVariables)
            startInfo.Environment[variable.Key] = variable.Value;

        startInfo.Environment["ROSEMARY_PREFIX"] = "off";
        startInfo.Environment["ROSEMARY_DECORATIONS"] = "server";
        startInfo.Environment["ROSEMARY_MENU"] = "none";
        startInfo.Environment["ROSEMARY_SCREEN_REFRESH_RATE"] = "9999";

        startInfo.ArgumentList.Add(install.ExecutablePath!);

        string launch = request.CommandLine?.Trim() ?? string.Empty;

        if (launch.Length == 0 && request.Mode == LaunchMode.Player)
            launch = "roblox-player:";

        foreach (string argument in SplitArguments(launch))
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;

        App.Logger.Info($"Started Rosemary (PID {process.Id})");

        return Task.FromResult(new LaunchResult
        {
            Started = true,
            Pid = process.Id
        });
    }

    private static List<string> SplitArguments(string commandLine)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;

        foreach (char character in commandLine)
        {
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (current.Length > 0)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
            arguments.Add(current.ToString());

        return arguments;
    }
}
