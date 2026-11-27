// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;
using Froststrap.Clients.Model;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Mods;

internal sealed record ModContext
{
    public required ModPaths ModPaths { get; init; }

    public required string VersionDirectory { get; init; }

    public required string VersionGuid { get; init; }

    public required ClientTarget Target { get; init; }

    public required IAppData AppData { get; init; }

    public required IReadOnlyDictionary<string, string> PackageDirectoryMap { get; init; }
}

internal sealed class ModEngine(
    ModContext context,
    Action<string> setStatus,
    Func<Dictionary<string, List<string>>, Task> restoreFromPackages,
    CancellationToken cancellationToken)
{
    private static readonly JsonSerializerOptions _indentedJsonOptions = new() { WriteIndented = true };

    private const string AppSettings =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
        "<Settings>\r\n" +
        "	<ContentFolder>content</ContentFolder>\r\n" +
        "	<BaseUrl>http://www.roblox.com</BaseUrl>\r\n" +
        "</Settings>\r\n";

    public async Task<bool> ApplyAsync()
    {
        bool success = true;
        setStatus(Strings.Bootstrapper_Status_ApplyingModifications);
        App.Logger.Info("Checking file mods...");

        File.Delete(Path.Combine(Paths.Base, "ModManifest.txt"));

        Directory.CreateDirectory(Paths.Modifications);

        var allMods = App.State.Prop.Mods.ToList();
        var allModFolderNames = allMods.Select(m => m.FolderName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activeMods = App.State.Prop.Mods
            .Where(m => m.Enabled && (
                m.Target == ModTarget.Both ||
                (context.Target == ClientTarget.Studio && m.Target == ModTarget.Studio) ||
                (context.Target == ClientTarget.Player && m.Target == ModTarget.Player)))
            .OrderByDescending(x => x.Priority)
            .ToList();

        ModPaths modPaths = context.ModPaths;
        string contentDirectory = modPaths.ContentRoot;
        string clientSettingsBaseDirectory = modPaths.ClientSettingsRoot;

        if (modPaths.Restore == RestoreStrategy.VersionBackup)
            EnsureMacResourcesBackup(contentDirectory, context.VersionGuid);

        var currentModManifest = new Dictionary<string, ModFileEntry>(StringComparer.OrdinalIgnoreCase);

        string modFontFamiliesFolder = Path.Combine(Paths.Modifications, "content", "fonts", "families");

        string? customFontPath = null;
        string? customFontFilename = null;
        string? customFontModFolder = null;

        foreach (var mod in activeMods)
        {
            string modTtf = Path.Combine(Paths.Modifications, mod.FolderName, "content", "fonts", "CustomFont.ttf");
            string modOtf = Path.Combine(Paths.Modifications, mod.FolderName, "content", "fonts", "CustomFont.otf");

            if (File.Exists(modTtf))
            {
                customFontPath = modTtf;
                customFontFilename = "CustomFont.ttf";
                customFontModFolder = mod.FolderName;
                break;
            }
            else if (File.Exists(modOtf))
            {
                customFontPath = modOtf;
                customFontFilename = "CustomFont.otf";
                customFontModFolder = mod.FolderName;
                break;
            }
        }

        if (customFontPath == null && File.Exists(Paths.CustomFont))
        {
            customFontPath = Paths.CustomFont;
            customFontFilename = "CustomFont.ttf";
        }

        if (customFontPath != null && customFontFilename != null)
        {
            string fontFamiliesFolder;
            if (customFontModFolder != null)
            {
                fontFamiliesFolder = Path.Combine(Paths.Modifications, customFontModFolder, "content", "fonts", "families");
            }
            else
            {
                fontFamiliesFolder = Path.Combine(Paths.Modifications, "content", "fonts", "families");
            }

            App.Logger.Info($"Begin font check using '{customFontFilename}' from '{customFontPath}' saving to '{fontFamiliesFolder}'");
            Directory.CreateDirectory(fontFamiliesFolder);

            string contentFolder = Path.Combine(contentDirectory, "content");
            Directory.CreateDirectory(contentFolder);
            string fontsFolder = Path.Combine(contentFolder, "fonts");
            Directory.CreateDirectory(fontsFolder);
            string familiesFolder = Path.Combine(fontsFolder, "families");
            Directory.CreateDirectory(familiesFolder);

            string rbxAssetPath = $"rbxasset://fonts/{customFontFilename}";

            foreach (string jsonFilePath in Directory.GetFiles(familiesFolder))
            {
                string jsonFilename = Path.GetFileName(jsonFilePath);
                string modFilepath = Path.Combine(fontFamiliesFolder, jsonFilename);
                if (File.Exists(modFilepath))
                    continue;
                var fontFamilyData = JsonHelper.Deserialize<FontFamily>(await File.ReadAllTextAsync(jsonFilePath));
                if (fontFamilyData is null)
                    continue;
                bool shouldWrite = false;
                foreach (var fontFace in fontFamilyData.Faces)
                {
                    if (fontFace.AssetId != rbxAssetPath)
                    {
                        fontFace.AssetId = rbxAssetPath;
                        shouldWrite = true;
                    }
                }
                if (shouldWrite)
                    await File.WriteAllTextAsync(modFilepath, JsonHelper.Serialize(fontFamilyData, _indentedJsonOptions));
            }
            App.Logger.Info("End font check");
        }
        else
        {
            string flatFontFamiliesFolder = Path.Combine(Paths.Modifications, "content", "fonts", "families");
            if (Directory.Exists(flatFontFamiliesFolder))
            {
                Directory.Delete(flatFontFamiliesFolder, true);
            }
        }

        App.Logger.Info("Writing AppSettings.xml...");
        if (!File.Exists(Path.Combine(Paths.Modifications, "AppSettings.xml"))
            && modPaths.Support == ModSupport.BundleFiles)
        {
            Directory.CreateDirectory(context.VersionDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(context.VersionDirectory, "AppSettings.xml"),
                AppSettings.Replace("roblox.com", Deployment.RobloxDomain, StringComparison.Ordinal)
            );
        }

        var allModFiles = new Dictionary<string, (string SourcePath, int Priority, string ModName, FileInfo Info)>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in activeMods)
        {
            string modSource = Path.Combine(Paths.Modifications, mod.FolderName);
            if (!Directory.Exists(modSource))
            {
                App.Logger.Warn($"Skipping mod '{mod.FolderName}': directory not found");
                continue;
            }

            App.Logger.Info($"Processing mod '{mod.FolderName}' (priority: {mod.Priority})");

            foreach (string file in Directory.GetFiles(modSource, "*.*", SearchOption.AllDirectories))
            {
                string relativeFile = Path.GetRelativePath(modSource, file);

                if (relativeFile == "README.txt" ||
                    relativeFile.EndsWith("info.json", StringComparison.Ordinal) ||
                    relativeFile.EndsWith(".lock", StringComparison.Ordinal) ||
                    IsClientSettingsRelativePath(relativeFile))
                    continue;

                string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(relativeFile);
                if (fileNameWithoutExt != null && fileNameWithoutExt.EndsWith("_Delete", StringComparison.Ordinal))
                    continue;

                var info = new FileInfo(file);

                allModFiles[relativeFile] = (file, mod.Priority, mod.FolderName, info);
            }
        }

        if (Directory.Exists(Paths.Modifications))
        {
            App.Logger.Info("Processing PresetModifications (Flat folder)...");

            foreach (string file in Directory.GetFiles(Paths.Modifications))
            {
                string relativeFile = Path.GetFileName(file);
                if (ShouldSkipBaseModFile(relativeFile))
                    continue;

                var info = new FileInfo(file);
                allModFiles[relativeFile] = (file, int.MaxValue, "BaseModification", info);
            }

            foreach (string dir in Directory.GetDirectories(Paths.Modifications))
            {
                string dirName = Path.GetFileName(dir);
                if (allModFolderNames.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                    continue;

                if (!Directory.Exists(dir))
                {
                    App.Logger.Warn($"Skipping missing directory: {dir}");
                    continue;
                }

                foreach (string file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    string relativeFile = Path.GetRelativePath(Paths.Modifications, file);
                    if (ShouldSkipBaseModFile(relativeFile))
                        continue;

                    var info = new FileInfo(file);
                    allModFiles[relativeFile] = (file, int.MaxValue, "BaseModification", info);
                }
            }
        }

        var filesToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in activeMods)
        {
            string modSource = Path.Combine(Paths.Modifications, mod.FolderName);
            if (!Directory.Exists(modSource)) continue;

            foreach (string file in Directory.GetFiles(modSource, "*_Delete.*", SearchOption.AllDirectories))
            {
                string relativeFile = Path.GetRelativePath(modSource, file);
                string actualFile = relativeFile;
                string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(relativeFile);
                if (fileNameWithoutExt != null && fileNameWithoutExt.EndsWith("_Delete", StringComparison.Ordinal))
                {
                    string directory = Path.GetDirectoryName(relativeFile) ?? "";
                    string originalName = fileNameWithoutExt[..^7];
                    actualFile = Path.Combine(directory, originalName + Path.GetExtension(relativeFile));
                }
                filesToDelete.Add(actualFile);
            }
        }

        foreach (string relPath in filesToDelete)
            allModFiles.Remove(relPath);

        foreach (string relPath in filesToDelete)
        {
            string targetFile = Path.Combine(contentDirectory, relPath);
            if (File.Exists(targetFile))
            {
                Filesystem.AssertReadOnly(targetFile);
                File.Delete(targetFile);
                App.Logger.Info($"{relPath} deleted via _Delete flag");

                string? parentDir = Path.GetDirectoryName(targetFile);
                while (!string.IsNullOrEmpty(parentDir) &&
                       parentDir.TrimEnd(Path.DirectorySeparatorChar) != contentDirectory.TrimEnd(Path.DirectorySeparatorChar))
                {
                    if (Directory.Exists(parentDir) && !Directory.EnumerateFileSystemEntries(parentDir).Any())
                    {
                        Directory.Delete(parentDir);
                        parentDir = Path.GetDirectoryName(parentDir);
                    }
                    else break;
                }
            }

            lock (currentModManifest)
                currentModManifest[relPath + "_Delete"] = new ModFileEntry { Size = 0, LastModified = DateTime.Now };
        }

        var fileTasks = new List<Task<bool>>();
        using var semaphore = new SemaphoreSlim(8);

        foreach (var entry in allModFiles)
        {
            if (cancellationToken.IsCancellationRequested) return true;

            string relativeFile = entry.Key;
            var (sourceFile, priority, modName, sourceInfo) = entry.Value;
            string fileVersionFolder = Path.Combine(contentDirectory, relativeFile);

            fileTasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    bool needsCopy = true;

                    if (File.Exists(fileVersionFolder))
                    {
                        var targetInfo = new FileInfo(fileVersionFolder);

                        if (targetInfo.Length == sourceInfo.Length &&
                            targetInfo.LastWriteTime == sourceInfo.LastWriteTime)
                        {
                            needsCopy = false;
                        }
                        else
                        {
                            var sourceHashTask = Task.Run(() => FastHash.FromFile(sourceFile));
                            var targetHashTask = Task.Run(() => FastHash.FromFile(fileVersionFolder));
                            await Task.WhenAll(sourceHashTask, targetHashTask);

                            if (await sourceHashTask == await targetHashTask)
                            {
                                needsCopy = false;

                                File.SetLastWriteTime(fileVersionFolder, sourceInfo.LastWriteTime);
                            }
                        }
                    }

                    if (needsCopy)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fileVersionFolder)!);
                        Filesystem.AssertReadOnly(fileVersionFolder);
                        File.Copy(sourceFile, fileVersionFolder, true);
                        File.SetLastWriteTime(fileVersionFolder, sourceInfo.LastWriteTime);
                        Filesystem.AssertReadOnly(fileVersionFolder);
                        App.Logger.Info($"{relativeFile} applied");
                    }

                    lock (currentModManifest)
                    {
                        currentModManifest[relativeFile] = new ModFileEntry
                        {
                            Size = sourceInfo.Length,
                            LastModified = sourceInfo.LastWriteTime
                        };
                    }

                    return true;
                }

                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to apply ({relativeFile}) from mod '{modName}': {ex.Message}");
                    return false;
                }
                finally { semaphore.Release(); }
            }));
        }

        var fileResults = await Task.WhenAll(fileTasks);
        success = success && fileResults.All(r => r);

        if (App.Settings.Prop.UseFastFlagManager)
        {
            bool profileApplied = false;
            string source = Path.Combine(Paths.Modifications, "ClientSettings", "ClientAppSettings.json");
            string rel = Path.Combine("ClientSettings", "ClientAppSettings.json");
            string dest = Path.Combine(clientSettingsBaseDirectory, rel);

            if (!profileApplied && modPaths.Support == ModSupport.BundleFiles && File.Exists(source))
            {
                try
                {
                    bool match = File.Exists(dest) &&
                        (await Task.Run(() => FastHash.FromFile(source)) == await Task.Run(() => FastHash.FromFile(dest)));
                    if (!match)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        File.Copy(source, dest, true);
                        App.Logger.Info("FastFlags Applied (normal source).");
                    }

                    if (File.Exists(dest))
                    {
                        var info = new FileInfo(dest);
                        lock (currentModManifest)
                            currentModManifest[rel] = new ModFileEntry { Size = info.Length, LastModified = info.LastWriteTime };
                    }
                }
                catch (Exception ex) { App.Logger.Error(ex); }
            }
        }
        else
        {
            string rel = Path.Combine("ClientSettings", "ClientAppSettings.json");
            string dest = Path.Combine(clientSettingsBaseDirectory, rel);
            if (File.Exists(dest))
            {
                try
                {
                    File.Delete(dest);
                    lock (currentModManifest)
                        currentModManifest.Remove(rel);
                    App.Logger.Info("ClientSettings deleted because UseFastFlagManager is false.");
                }
                catch (Exception ex) { App.Logger.Error(ex); }
            }
        }

        var fileRestoreMap = new Dictionary<string, List<string>>();
        foreach (string fileLocation in context.AppData.DistributionState.ModManifest)
        {
            if (currentModManifest.ContainsKey(fileLocation))
                continue;

            string actualFile = fileLocation;
            string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileLocation);

            if (fileNameWithoutExt != null && fileNameWithoutExt.EndsWith("_Delete", StringComparison.Ordinal) && modPaths.Support != ModSupport.BundleFiles)
                continue;

            if (modPaths.Restore == RestoreStrategy.VersionBackup)
            {
                bool isClientSettingsFile = IsClientSettingsRelativePath(actualFile);
                string backupDir = GetResourcesBackupPath(context.VersionGuid);
                string sourceFile = Path.Combine(backupDir, actualFile);
                string targetRoot = isClientSettingsFile
                    ? clientSettingsBaseDirectory
                    : contentDirectory;
                string destFile = Path.Combine(targetRoot, actualFile);
                if (File.Exists(sourceFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    File.Copy(sourceFile, destFile, true);
                    App.Logger.Info($"Restored '{actualFile}' from backup");
                }
                else
                {
                    if (isClientSettingsFile && File.Exists(destFile))
                    {
                        File.Delete(destFile);
                        App.Logger.Info($"Deleted stale ClientSettings file '{actualFile}'");
                    }
                    else
                    {
                        App.Logger.Warn($"Backup file not found: {actualFile}");
                    }
                }
                continue;
            }

            string? packageName = null;
            string? packageDir = null;

            foreach (var kvp in context.PackageDirectoryMap)
            {
                if (!string.IsNullOrEmpty(kvp.Value) && actualFile.StartsWith(kvp.Value, StringComparison.OrdinalIgnoreCase))
                {
                    packageName = kvp.Key;
                    packageDir = kvp.Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageDir))
            {
                string versionFileLocation = Path.Combine(context.VersionDirectory, actualFile);
                if (File.Exists(versionFileLocation))
                {
                    Filesystem.AssertReadOnly(versionFileLocation);
                    File.Delete(versionFileLocation);
                    App.Logger.Info($"Deleted orphaned file {actualFile}");
                }
                continue;
            }

            string internalZipPath = actualFile[packageDir.Length..].TrimStart(Path.DirectorySeparatorChar);

            if (!fileRestoreMap.ContainsKey(packageName))
                fileRestoreMap[packageName] = [];

            fileRestoreMap[packageName].Add(internalZipPath);
            App.Logger.Info($"Restoring '{internalZipPath}' from package {packageName}");
        }

        if (modPaths.Restore == RestoreStrategy.PackageReextract && fileRestoreMap.Count > 0)
            await restoreFromPackages(fileRestoreMap);

        if (BackgroundUpdate.IsCurrentProcess || !context.AppData.DistributionStateManager.HasFileOnDiskChanged())
        {
            context.AppData.DistributionState.ModManifest = [.. currentModManifest.Keys];
            context.AppData.DistributionStateManager.Save();
        }

        App.Logger.Info("Finished checking file mods");
        return success;
    }

    public static void InitializeModFolders(string versionDirectory, ModPaths modPaths, string displayName)
    {
        if (string.IsNullOrEmpty(versionDirectory) || !Directory.Exists(versionDirectory))
        {
            App.Logger.Warn("Version directory does not exist, skipping.");
            return;
        }

        if (modPaths.Support != ModSupport.BundleFiles)
        {
            App.Logger.Info($"Skipping mod folder initialization, {displayName} does not take mod files.");
            return;
        }

        string contentRoot = modPaths.ContentRoot;
        if (!Directory.Exists(contentRoot))
        {
            App.Logger.Warn($"Content root not found: {contentRoot}, skipping.");
            return;
        }

        string[] topFolders = ["ExtraContent", "content", "PlatformContent"];

        string modsRoot = Paths.Modifications;
        Directory.CreateDirectory(modsRoot);

        App.Logger.Info("Initializing mod folders for ExtraContent, content, and PlatformContent...");

        foreach (string topFolder in topFolders)
        {
            string sourceFolder = Path.Combine(contentRoot, topFolder);
            if (!Directory.Exists(sourceFolder))
            {
                App.Logger.Warn($"Top folder '{topFolder}' not found, skipping.");
                continue;
            }

            var directories = Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories);
            var allDirs = new List<string> { topFolder };
            allDirs.AddRange(directories.Select(d => Path.GetRelativePath(contentRoot, d)));

            foreach (string relPath in allDirs)
            {
                string target = Path.Combine(modsRoot, relPath);
                Directory.CreateDirectory(target);
            }

            App.Logger.Info($"Mirrored {allDirs.Count} directories for '{topFolder}'.");
        }

        App.Logger.Info("Mod folder initialization complete.");
    }

    private static bool ShouldSkipBaseModFile(string relativeFile) =>
        relativeFile == "README.txt" ||
        relativeFile == "info.json" ||
        relativeFile.EndsWith(".lock", StringComparison.Ordinal) ||
        relativeFile.EndsWith(".dll", StringComparison.Ordinal) ||
        relativeFile.EndsWith(".exe", StringComparison.Ordinal) ||
        IsClientSettingsRelativePath(relativeFile);

    private static bool IsClientSettingsRelativePath(string relativePath)
    {
        string normalizedPath = relativePath.Replace('\\', '/');
        return normalizedPath.StartsWith("ClientSettings/", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetResourcesBackupPath(string versionGuid)
    {
        return Path.Combine(Paths.Base, "ModBackup", versionGuid);
    }

    private static void CopyDirectory(string sourceDir, string destDir, bool overwrite = true)
    {
        Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string dest = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite);
        }
    }

    private static void EnsureMacResourcesBackup(string resourcesDir, string versionGuid)
    {
        string backupDir = GetResourcesBackupPath(versionGuid);

        if (Directory.Exists(backupDir))
        {
            App.Logger.Info($"Resources backup for version {versionGuid} already exists.");
            return;
        }

        App.Logger.Info($"Creating Resources backup for version {versionGuid}...");
        Directory.CreateDirectory(backupDir);
        CopyDirectory(resourcesDir, backupDir, true);
        App.Logger.Info("Resources backup created.");
    }
}
