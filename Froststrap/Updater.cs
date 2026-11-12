// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

namespace Froststrap;

internal class Updater
{
    public static async Task RunMigrations(string? previousVersion = null)
    {
        if (OperatingSystem.IsLinux())
            SetupSoberSymlink();

        string currentVer = App.InternalVersion;
        string? existingVer = previousVersion ?? App.State.Prop.LastMigratedVersion;

        if (existingVer is null && !App.Settings.IsSaved)
        {
            App.Logger.Info($"Fresh install detected — stamping LastMigratedVersion as {currentVer}");
            App.State.Prop.LastMigratedVersion = currentVer;
            App.State.Save();
            return;
        }

        if (existingVer is null)
        {
            var legacyStateCheck = new JsonManager<RobloxState>();
            if (!legacyStateCheck.IsSaved)
            {
                App.Logger.Info("No LastMigratedVersion but no legacy data found — treating as already migrated");
                App.State.Prop.LastMigratedVersion = currentVer;
                App.State.Save();
                return;
            }

            App.Logger.Info("Legacy RobloxState data found — treating as pre-migration install");
            existingVer = "0.0.0";
        }

        App.Logger.Info($"Comparing migration versions: existing='{existingVer}', current='{currentVer}'");
        if (Utility.Versioning.CompareVersions(existingVer, currentVer) != VersionComparison.LessThan)
        {
            App.Logger.Info($"Migrations up to date (last={existingVer}, current={currentVer})");
            return;
        }

        App.Logger.Info($"Running migrations: {existingVer} -> {currentVer}");

        if (Utility.Versioning.CompareVersions(existingVer, "1.4.0.0") == VersionComparison.LessThan)
        {
            JsonManager<RobloxState> legacyRobloxState = new();

            if (legacyRobloxState.IsSaved)
            {
                if (legacyRobloxState.Load(false))
                {
                    App.PlayerState.Prop.VersionGuid = legacyRobloxState.Prop.Player.VersionGuid;
                    App.PlayerState.Prop.PackageHashes = legacyRobloxState.Prop.Player.PackageHashes;
                    App.PlayerState.Prop.ModManifest = legacyRobloxState.Prop.ModManifest;

                    App.StudioState.Prop.VersionGuid = legacyRobloxState.Prop.Studio.VersionGuid;
                    App.StudioState.Prop.PackageHashes = legacyRobloxState.Prop.Studio.PackageHashes;
                }

                legacyRobloxState.Delete();
            }

            if (App.Settings.Prop.Theme == Theme.Custom)
                App.Settings.Prop.Theme = Theme.Default;

            TryDelete(Path.Combine(Paths.Cache, "GameHistory.json"));
        }
        if (Utility.Versioning.CompareVersions(existingVer, "1.4.2") == VersionComparison.LessThan)
        {
            string genCacheDir = Path.Combine(Path.GetTempPath(), "Froststrap", "mod-generator");
            string pluginCacheDir = Path.Combine(Paths.Roblox, "Plugins", "FroststrapStudioRPC.rbxmx");

            if (Directory.Exists(genCacheDir))
            {
                Directory.Delete(genCacheDir, true);
                App.Logger.Info("Deleted mod-generator cache for migration.");
            }

            if (Directory.Exists(pluginCacheDir))
            {
                Directory.Delete(pluginCacheDir, true);
                App.Logger.Info("Deleted studio plugin for migration.");
            }

            TryDelete(Path.Combine(Paths.Cache, "channelCache.json"));
            TryDelete(Path.Combine(Paths.Cache, "channelCacheMeta.json"));
            TryDelete(Path.Combine(Paths.Cache, "datacenters_cache.json"));
        }

        if (Utility.Versioning.CompareVersions(existingVer, "1.5.1") == VersionComparison.LessThan)
        {
            App.Settings.Prop.BootstrapperStyle = BootstrapperStyle.FluentAeroDialog;
            App.Settings.Prop.SelectedBackdrop = WindowsBackdrops.None;
        }

        App.State.Prop.LastMigratedVersion = currentVer;
        App.State.Save();

        if (App.Settings.Loaded) App.Settings.Save();
        if (App.PlayerState.Loaded) App.PlayerState.Save();
        if (App.StudioState.Loaded) App.StudioState.Save();

        App.Logger.Info($"Migrations complete — LastMigratedVersion set to {currentVer}");
    }

    [SupportedOSPlatform("linux")]
    private static async void SetupSoberSymlink()
    {
        string flatpakId = "org.vinegarhq.Sober";
        string flatpakDataPath = Path.Combine(Paths.UserProfile, ".var", "app", flatpakId);
        string soberTarget = Path.Combine(Paths.Versions, "Sober");

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

    [SupportedOSPlatform("linux")]
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

    [SupportedOSPlatform("linux")]
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

    [SupportedOSPlatform("windows")]
    public static void MigrateOutOfInstallRoot()
    {
        string legacyRoot = Paths.WindowsInstallRoot;

        if (!Directory.Exists(legacyRoot))
            return;

        foreach (string source in Directory.EnumerateFileSystemEntries(legacyRoot))
        {
            string name = Path.GetFileName(source);

            if (IsInstallerOwned(source, name))
                continue;

            string dest = Path.Combine(Paths.DataRoot, name);

            try
            {
                MoveEntry(source, dest);
                App.Logger.Info($"Migrated '{source}' -> '{dest}'");
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"Failed to migrate '{source}' -> '{dest}'");
            }
        }
    }

    private static bool IsInstallerOwned(string path, string name)
    {
        if (Directory.Exists(path))
            return name.Equals("current", StringComparison.OrdinalIgnoreCase)
                || name.Equals("packages", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Locks", StringComparison.OrdinalIgnoreCase);

        string ext = Path.GetExtension(name);
        return ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pdb", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".nupkg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".version", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith('.')
            || name.StartsWith("velopack", StringComparison.OrdinalIgnoreCase);
    }

    private static void MoveEntry(string source, string dest)
    {
        if (File.Exists(source))
        {
            if (File.Exists(dest))
            {
                App.Logger.Warn($"'{dest}' already exists, leaving '{source}' in place");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Move(source, dest);
            return;
        }

        var info = new DirectoryInfo(source);

        if (Directory.Exists(dest) && !Directory.EnumerateFileSystemEntries(dest).Any())
            Directory.Delete(dest);

        if (info.LinkTarget is not null)
        {
            string? target = info.ResolveLinkTarget(true)?.FullName;

            if (Directory.Exists(dest))
            {
                App.Logger.Warn($"'{dest}' already has data, leaving junction '{source}' in place");
                return;
            }

            if (target is not null)
                StorageLocation.CreateLink(dest, target);

            info.Delete();
            return;
        }

        if (!Directory.Exists(dest))
        {
            try
            {
                Directory.Move(source, dest);
                return;
            }
            catch (IOException) when (Directory.Exists(source) && !Directory.Exists(dest)) { }
        }

        MergeDirectory(source, dest);
    }

    private static void MergeDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);

        foreach (string file in Directory.EnumerateFiles(source))
        {
            string target = Path.Combine(dest, Path.GetFileName(file));
            if (!File.Exists(target))
                File.Move(file, target);
        }

        foreach (string dir in Directory.EnumerateDirectories(source))
            MoveEntry(dir, Path.Combine(dest, Path.GetFileName(dir)));

        if (!Directory.EnumerateFileSystemEntries(source).Any())
            Directory.Delete(source);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort */ }
    }
}
