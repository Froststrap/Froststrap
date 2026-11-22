// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Bootstrapping;

internal static class CustomIntegrationLauncher
{
    public static async Task LaunchPreLaunchAsync(List<int> autoclosePids)
    {
        foreach (var integration in App.Settings.Prop.CustomIntegrations)
        {
            if (integration?.PreLaunch == true)
                await LaunchAsync(integration, autoclosePids);
        }
    }

    public static async Task LaunchPostLaunchAsync(List<int> autoclosePids)
    {
        foreach (var integration in App.Settings.Prop.CustomIntegrations)
        {
            if (integration == null || integration.PreLaunch || integration.SpecifyGame)
                continue;

            await LaunchAsync(integration, autoclosePids);
        }
    }

    public static async Task LaunchAsync(CustomIntegration integration, List<int> autoclosePids)
    {
        App.Logger.Info($"Launching custom integration '{integration.Name}' ({integration.Location} {integration.LaunchArgs} - autoclose is {integration.AutoClose})");

        int pid = 0;

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = integration.Location,
                Arguments = integration.LaunchArgs.Replace("\r\n", " ", StringComparison.Ordinal),
                WorkingDirectory = Path.GetDirectoryName(integration.Location),
                UseShellExecute = true
            })!;

            pid = process.Id;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to launch integration '{integration.Name}'! {ex.Message}");
        }

        if (integration.AutoClose && pid != 0)
            autoclosePids.Add(pid);

        if (integration.Delay != 0)
            await Task.Delay(integration.Delay);
    }
}
