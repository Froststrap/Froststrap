// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Froststrap.Utility;

[SupportedOSPlatform("windows")]
static class WindowsRegistry
{
    private const string RobloxPlaceKey = "Roblox.Place";

    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private const string UninstallKey = $@"{UninstallRoot}\MSI:{ProjectInfo.Name}";

    [Obsolete("LegacyUninstallKey is obsolete and is currently only used new version migration."
            +"Delete all references after v2.0.9 release",
            DiagnosticId = "FS0001"
    )]
    private const string LegacyUninstallKey = $@"{UninstallRoot}\{ProjectInfo.Name}";

    public static readonly List<RegistryKey> Roots = [Registry.CurrentUser, Registry.LocalMachine];

    public static void RegisterProtocol(string key, string name, string handler, string handlerParam = "%1")
    {
        if (!OperatingSystem.IsWindows())
            return;

        string handlerArgs = $"\"{handler}\" {handlerParam}";

        using var uriKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{key}");
        using var uriIconKey = uriKey.CreateSubKey("DefaultIcon");
        using var uriCommandKey = uriKey.CreateSubKey(@"shell\open\command");

        if (uriKey.GetValue("") is null)
        {
            uriKey.SetValueSafe("", $"URL: {name} Protocol");
            uriKey.SetValueSafe("URL Protocol", "");
        }

        if (uriCommandKey.GetValue("") as string != handlerArgs)
        {
            uriIconKey.SetValueSafe("", handler);
            uriCommandKey.SetValueSafe("", handlerArgs);
        }
    }

    /// <summary>
    /// Registers Roblox Player protocols for Froststrap
    /// </summary>
    public static void RegisterPlayer() => RegisterPlayer(Paths.Process, LaunchEntry.GetCliArgs(LaunchMode.Player, "%1"));

    public static void RegisterPlayer(string handler, string handlerParam)
    {
        if (!OperatingSystem.IsWindows())
            return;

        RegisterProtocol("roblox", "Roblox", handler, handlerParam);
        RegisterProtocol("roblox-player", "Roblox", handler, handlerParam);
    }

    /// <summary>
    /// Registers all Roblox Studio classes for Froststrap
    /// </summary>
    public static void RegisterStudio()
    {
        if (!OperatingSystem.IsWindows())
            return;

        RegisterStudioProtocol(Paths.Process, LaunchEntry.GetCliArgs(LaunchMode.Studio, "%1"));
        RegisterStudioFileClass(Paths.Process, LaunchEntry.GetCliArgs(LaunchMode.Studio, "%1"));
        RegisterStudioFileTypes();
    }

    /// <summary>
    /// Registers roblox-studio and roblox-studio-auth protocols
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="handlerParam"></param>
    public static void RegisterStudioProtocol(string handler, string handlerParam)
    {
        if (!OperatingSystem.IsWindows())
            return;

        RegisterProtocol("roblox-studio", "Roblox", handler, handlerParam);
        RegisterProtocol("roblox-studio-auth", "Roblox", handler, handlerParam);
    }

    /// <summary>
    /// Registers file associations for Roblox.Place class
    /// </summary>
    public static void RegisterStudioFileTypes()
    {
        if (!OperatingSystem.IsWindows())
            return;

        RegisterStudioFileType(".rbxl");
        RegisterStudioFileType(".rbxlx");
        RegisterStudioFileType(".rbxm");
        RegisterStudioFileType(".rbxmx");
    }

    /// <summary>
    /// Registers Roblox.Place class
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="handlerParam"></param>
    public static void RegisterStudioFileClass(string handler, string handlerParam)
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string keyValue = "Roblox Place";
        string handlerArgs = $"\"{handler}\" {handlerParam}";
        string iconValue = $"{handler},0";

        using RegistryKey uriKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + RobloxPlaceKey);
        using RegistryKey uriIconKey = uriKey.CreateSubKey("DefaultIcon");
        using RegistryKey uriOpenKey = uriKey.CreateSubKey(@"shell\Open");
        using RegistryKey uriCommandKey = uriOpenKey.CreateSubKey(@"command");

        if (uriKey.GetValue("") as string != keyValue)
            uriKey.SetValueSafe("", keyValue);

        if (uriCommandKey.GetValue("") as string != handlerArgs)
            uriCommandKey.SetValueSafe("", handlerArgs);

        if (uriOpenKey.GetValue("") as string != "Open")
            uriOpenKey.SetValueSafe("", "Open");

        if (uriIconKey.GetValue("") as string != iconValue)
            uriIconKey.SetValueSafe("", iconValue);
    }

    public static void RegisterStudioFileType(string key)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using RegistryKey uriKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{key}");
        uriKey.CreateSubKey(RobloxPlaceKey + @"\ShellNew");

        if (uriKey.GetValue("") as string != RobloxPlaceKey)
            uriKey.SetValueSafe("", RobloxPlaceKey);
    }

    public static void RegisterClientLocation(bool isStudio, string? clientPath)
    {
        if (!OperatingSystem.IsWindows())
            return;

        string keyName = isStudio ? "StudioPath" : "PlayerPath";
        clientPath ??= "";

        using var apisKey = Registry.CurrentUser.CreateSubKey(ProjectInfo.RegistryKey);
        apisKey.SetValueSafe(keyName, clientPath);
    }

    public static void Unregister(string key)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{key}");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to unregister {key}: {ex}");
        }
    }

    public static void SyncUninstallEntry()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string version = App.Version;

        if (string.IsNullOrWhiteSpace(version) || version is "unknown" || version.StartsWith("0.0.0", StringComparison.Ordinal))
            return;

        if (UpdaterManager.IsCiBuild)
            return;

        try
        {
            using var uninstallKey = OpenUninstallKey(UninstallKey);

            if (uninstallKey is not null)
            {
                uninstallKey.SetValue("DisplayVersion", version);
                RemoveLegacyUninstallEntry();
                return;
            }

            using var legacyKey = OpenUninstallKey(LegacyUninstallKey);
            legacyKey?.SetValue("DisplayVersion", version);
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to reconcile uninstall registry entry");
        }
    }

    private static void RemoveLegacyUninstallEntry()
    {
        foreach (var root in Roots)
        {
            try
            {
                using (var legacyKey = root.OpenSubKey(LegacyUninstallKey))
                {
                    if (legacyKey is null)
                        continue;
                }

                root.DeleteSubKeyTree(LegacyUninstallKey, throwOnMissingSubKey: false);
                App.Logger.Info($"Removed stale legacy uninstall registry entry from {root.Name}");
            }
            catch (Exception ex)
            {
                App.Logger.Debug(ex, $"Failed to remove legacy uninstall registry entry from {root.Name}");
            }
        }
    }

    private static RegistryKey? OpenUninstallKey(string subKeyPath)
    {
        try
        {
            return Registry.CurrentUser.OpenSubKey(subKeyPath, writable: true)
                ?? Registry.LocalMachine.OpenSubKey(subKeyPath, writable: true);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to open uninstall registry key '{subKeyPath}': {ex.Message}");
            return null;
        }
    }

    private static RegistryKey? OpenActiveUninstallKey()
        => OpenUninstallKey(UninstallKey) ?? OpenUninstallKey(LegacyUninstallKey);

    public static void UpdateEstimatedSize()
    {
        if (!OperatingSystem.IsWindows())
            return;

        _ = Task.Run(() =>
        {
            try
            {
                long totalBytes = 0;

                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                foreach (var file in new DirectoryInfo(Paths.Base).EnumerateFiles("*", options))
                    totalBytes += file.Length;

                int totalKB = (int)Math.Min(totalBytes / 1024, int.MaxValue);

                using var uninstallKey = OpenActiveUninstallKey();
                if (uninstallKey is null)
                {
                    App.Logger.Debug("No uninstall registry entry to update EstimatedSize on");
                    return;
                }

                uninstallKey.SetValue("EstimatedSize", totalKB);
                App.Logger.Info($"Updated EstimatedSize to {totalKB} KB");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to update size: {ex.Message}");
            }
        });
    }
}
