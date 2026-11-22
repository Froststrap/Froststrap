// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Launchers;
using Froststrap.Clients.Providers;
using Froststrap.Clients.Model;
using Froststrap.Clients.Registry;
using Froststrap.Clients.Runtime;
using Froststrap.Clients.Sources;
using Froststrap.Utility;

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
    public override bool IsInstalled()
    {
        string clientPath = Path.Combine(Install.InstallRoot, "data", "sober", "packages", "x86_64", "com.roblox.client");

        try
        {
            return Directory.Exists(clientPath)
                && Directory.EnumerateFiles(clientPath, "*", SearchOption.AllDirectories).Any();
        }
        catch (Exception)
        {
            return false;
        }
    }

    public const string FlatpakId = "org.vinegarhq.Sober";

    public override IClientLauncher Launcher => _soberLauncher ??= new SoberLauncher(Layout, (FlatpakProcessService)Processes, SettingsSync!);

    public override IProcessService Processes => _soberProcesses ??= new FlatpakProcessService(Layout, FlatpakId);

    private FlatpakProcessService? _soberProcesses;

    private SoberLauncher? _soberLauncher;

    public override ISettingsSync? SettingsSync { get; } = new SoberSettingsSync();

    public override string LaunchLabel => Strings.Common_Sober;

    private static FlatpakSource Source => new(FlatpakId);

    public override async Task<InstallResult> EnsureInstalledAsync(IInstallReporter reporter)
    {
        reporter.SetStatus(Strings.Bootstrapper_Status_CheckingFlatpak);

        try
        {
            await FlatpakSource.EnsureFlatpakAvailableAsync();
        }
        catch (TimeoutException ex)
        {
            App.Logger.Error(ex, "Timed out while checking Flatpak installation.");
            return new InstallResult { Error = "Timed out while checking Flatpak installation. Please make sure Flatpak is working and try again." };
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Flatpak not found.");
            return new InstallResult { Error = "Flatpak is required on Linux.\n\nPlease install Flatpak first, then launch Froststrap again." };
        }

        if (await Source.IsInstalledAsync())
        {
            App.Logger.Info("Sober is already installed.");
            return new InstallResult { Success = true };
        }

        App.Logger.Info("Installing Sober...");

        reporter.SetIndeterminate(includeTaskbar: true);
        reporter.SetStatus(Strings.Bootstrapper_Status_InstallingSober);

        InstallResult result = await Source.InstallAsync(
            new InlineProgress<InstallProgress>(step => reporter.SetStatus(step.Message)));

        if (!result.Success)
        {
            string message = result.Error is null
                ? "Failed to start Sober installation via Flatpak."
                : string.Format(CultureInfo.InvariantCulture, Strings.Sober_FlatpakInstallFailed, FlatpakId, string.IsNullOrWhiteSpace(result.Error) ? "" : $"\n\n{result.Error}");

            return new InstallResult { Error = message };
        }

        App.Logger.Info("Sober installation complete.");
        return new InstallResult { Success = true };
    }

    public override async Task UpdateAsync(IInstallReporter reporter, CancellationToken cancellationToken = default)
    {
        if (!App.Settings.Prop.AutomaticallyUpdateSober)
            return;

        reporter.SetStatus(Strings.Bootstrapper_Status_UpdatingSober);
        reporter.SetProgress(0);

        try
        {
            await Source.UpdateAsync(new InlineProgress<InstallProgress>(progress => OnUpdateProgress(reporter, progress)), cancellationToken);
        }
        finally
        {
            reporter.ClearProgress();
        }
    }

    private static void OnUpdateProgress(IInstallReporter reporter, InstallProgress progress)
    {
        if (progress.Phase == InstallPhase.Finalizing)
        {
            reporter.SetProgress(100);
            reporter.SetStatus(Strings.Bootstrapper_Status_SoberUpdateComplete);
            return;
        }

        if (progress.Step <= 0)
        {
            reporter.SetStatus(progress.Message);
            return;
        }

        reporter.SetProgress(progress.Current);

        reporter.SetStatus(progress.StepPercent >= 0
            ? string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_UpdatingSoberProgress, progress.Step, progress.StepCount, progress.StepPercent)
            : string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_UpdatingSoberBasic, progress.Step, progress.StepCount));
    }

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

        Directory.CreateDirectory(soberTarget, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

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
