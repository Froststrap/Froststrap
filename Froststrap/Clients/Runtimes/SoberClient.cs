// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Launchers;
using Froststrap.Clients.Providers;
using Froststrap.Clients.Model;
using Froststrap.Clients.Registry;
using Froststrap.Clients.Runtime;

namespace Froststrap.Clients.Runtimes;

/// <summary>
/// Sober is installed and updated by flatpak, so "is it installed" is a different question
/// from "is there a version directory". It is answered here rather than in shared code.
/// </summary>
internal sealed class SoberClient(
    IClientLayout layout,
    ClientCapabilities capabilities,
    string displayName,
    ClientEnvironment environment,
    IClientStateStore stateStore,
    string stateFileName)
    : RobloxClient(layout, capabilities, displayName, environment, stateStore, stateFileName)
{
    public override ClientInspection Inspect()
    {
        ClientInstall install = Install;
        string clientPath = Path.Combine(install.InstallRoot, "data", "sober", "packages", "x86_64", "com.roblox.client");

        bool installed;
        try
        {
            installed = Directory.Exists(clientPath)
                && Directory.EnumerateFiles(clientPath, "*", SearchOption.AllDirectories).Any();
        }
        catch (Exception ex)
        {
            return new ClientInspection
            {
                Install = install,
                IsInstalled = false,
                IsStateSaved = IsStateSaved,
                Reason = InstallReason.Missing,
                Error = ex.Message
            };
        }

        return new ClientInspection
        {
            Install = install,
            IsInstalled = installed,
            IsStateSaved = IsStateSaved,
            Reason = installed ? InstallReason.None : InstallReason.Missing
        };
    }

    public const string FlatpakId = "org.vinegarhq.Sober";

    public override IClientLauncher Launcher => _soberLauncher ??= new SoberLauncher(Layout, (FlatpakProcessService)Processes, SettingsSync!);

    public override IProcessService Processes => _soberProcesses ??= new FlatpakProcessService(Layout, FlatpakId);

    private FlatpakProcessService? _soberProcesses;

    private SoberLauncher? _soberLauncher;

    public override ISettingsSync? SettingsSync { get; } = new SoberSettingsSync();

    public override string LaunchLabel => Strings.Common_Sober;

    public override string StartingStatus => Strings.Bootstrapper_Status_StartingSober;

    public override string DescribeLaunchFailure(string details) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Sober_LaunchFailed, FlatpakId, details);

    public override bool ResetInstalledPackages()
    {
        string clientPackagePath = Path.Combine(Install.InstallRoot, "data", "sober", "packages", "x86_64", "com.roblox.client");

        try
        {
            if (Directory.Exists(clientPackagePath))
            {
                DirectoryInfo di = new(clientPackagePath);

                foreach (FileInfo file in di.GetFiles())
                    file.Delete();

                foreach (DirectoryInfo dir in di.GetDirectories())
                    dir.Delete(true);

                App.Logger.Debug($"Successfully cleared contents of {clientPackagePath}");
                return true;
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to purge packages: {ex.Message}");
        }

        return false;
    }

    public override async Task PrepareEnvironmentAsync()
    {
        string flatpakDataPath = Path.Combine(Paths.UserProfile, ".var", "app", FlatpakId);
        string soberTarget = Install.InstallRoot;

        if (IsSymlinkPointingAt(flatpakDataPath, soberTarget))
        {
            App.Logger.Info("Sober symlink already in place, skipping.");
            return;
        }

        App.Logger.Info($"Setting up Sober symlink: {flatpakDataPath} -> {soberTarget}");

        Directory.CreateDirectory(soberTarget);

        if (Directory.Exists(flatpakDataPath) && !IsSymlink(flatpakDataPath))
        {
            App.Logger.Info($"Copying existing Sober data from {flatpakDataPath} to {soberTarget}");
            await Utility.Threading.RunAsync("cp", $"-a \"{flatpakDataPath}/.\" \"{soberTarget}/\"");

            App.Logger.Info($"Removing original Sober data directory at {flatpakDataPath}");
            await Utility.Threading.RunAsync("rm", $"-rf \"{flatpakDataPath}\"");
        }
        else if (IsSymlink(flatpakDataPath))
        {
            App.Logger.Info($"Removing stale symlink at {flatpakDataPath}");
            Directory.Delete(flatpakDataPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(flatpakDataPath)!);

        Directory.CreateSymbolicLink(flatpakDataPath, soberTarget);
        App.Logger.Info($"Created symlink: {flatpakDataPath} -> {soberTarget}");
    }

    private static bool IsSymlink(string path)
    {
        if (!Path.Exists(path))
            return false;

        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch { return false; }
    }

    private static bool IsSymlinkPointingAt(string path, string expectedTarget)
    {
        if (!IsSymlink(path))
            return false;

        try
        {
            string? actual = Directory.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName;
            return actual == expectedTarget;
        }
        catch { return false; }
    }
}
