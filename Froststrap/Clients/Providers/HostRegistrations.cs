// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.Versioning;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;
using Froststrap.Utility;
using Microsoft.Win32;

namespace Froststrap.Clients.Providers;

[SupportedOSPlatform("windows")]
internal sealed class WindowsHostRegistration(IClientLayout layout) : IHostRegistration
{
    private const string AppCompatLayersKey = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers";

    private bool IsStudio => layout.Target == ClientTarget.Studio;

    public void Register(string? versionDirectory)
    {
        if (IsStudio)
            WindowsRegistry.RegisterStudio();
        else
            WindowsRegistry.RegisterPlayer();

        WindowsRegistry.RegisterClientLocation(IsStudio, versionDirectory);
    }

    public void ClearLocation() => WindowsRegistry.RegisterClientLocation(IsStudio, null);

    public void RemoveStartupEntry()
    {
        string valueName = layout.ProcessNames[0];

        try
        {
            using var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

            if (runKey?.GetValue(valueName) is not string entry)
                return;

            // make sure we only ever delete roblox's own entry
            if (!entry.Contains(layout.ExecutableName!, StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.Info($"Leaving '{valueName}' alone, it doesn't point at Roblox");
                return;
            }

            App.Logger.Info($"Removing startup entry '{entry}'");
            runKey.DeleteValue(valueName);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to remove startup entry: {ex}");
        }
    }

    public bool RequestsAdmin(string executablePath)
    {
        foreach (var root in WindowsRegistry.Roots)
        {
            using var key = root.OpenSubKey(AppCompatLayersKey);

            if (key is null)
                continue;

            string? flags = (string?)key.GetValue(executablePath);

            if (flags is not null && flags.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public void MigrateCompatibilityFlags(string oldExecutablePath, string newExecutablePath)
    {
        using RegistryKey appFlagsKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(AppCompatLayersKey);
        string? appFlags = appFlagsKey.GetValue(oldExecutablePath) as string;

        if (appFlags is not null)
        {
            App.Logger.Info($"Migrating app compatibility flags from {oldExecutablePath} to {newExecutablePath}...");
            appFlagsKey.SetValueSafe(newExecutablePath, appFlags);
            appFlagsKey.DeleteValueSafe(oldExecutablePath);
        }
    }
}

internal sealed class NullHostRegistration : IHostRegistration
{
    public void Register(string? versionDirectory)
    {
    }

    public void ClearLocation()
    {
    }

    public void RemoveStartupEntry()
    {
    }

    public bool RequestsAdmin(string executablePath) => false;

    public void MigrateCompatibilityFlags(string oldExecutablePath, string newExecutablePath)
    {
    }
}

internal static class HostRegistrations
{
    public static IHostRegistration Create(IClientLayout layout) => layout.HostIntegration switch
    {
        HostIntegration.WindowsRegistry when OperatingSystem.IsWindows() => new WindowsHostRegistration(layout),
        _ => new NullHostRegistration()
    };
}
