// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Win32;
using System.IO.Compression;
using Froststrap.RobloxInterfaces;

namespace Froststrap;

internal partial class Bootstrapper : IDisposable
{
    private async Task SetupPackageDictionaries()
    {
        if (OperatingSystem.IsMacOS())
        {
            PackageDirectoryMap = new Dictionary<string, string>
            {
                { "RobloxPlayer.zip", "" },
                { "RobloxStudioApp.zip", "" }
            };
            return;
        }
        else if (OperatingSystem.IsLinux() && !IsStudioLaunch)
        {
            PackageDirectoryMap = [];
            return;
        }

        await App.RemoteData.WaitUntilDataFetched();

        var localData = App.RemoteData.Prop.PackageMaps[IsStudioLaunch ? "studio" : "player"];
        var commonData = App.RemoteData.Prop.PackageMaps.CommonPackageMap;

        PackageDirectoryMap = new(commonData);

        foreach (var package in localData)
            PackageDirectoryMap[package.Key] = package.Value;

        if (OperatingSystem.IsWindows())
            return;

        foreach (var key in PackageDirectoryMap.Keys.ToList())
        {
            if (PackageDirectoryMap[key] != null)
                PackageDirectoryMap[key] = PackageDirectoryMap[key].Replace('\\', '/');
        }
    }

    private async Task UpgradeRoblox()
    {
        if (!App.Settings.Prop.UpdateRoblox)
        {
            SetStatus(Strings.Bootstrapper_Status_CancelUpgrade);
            App.Logger.Info("Upgrading disabled, cancelling the upgrade.");

            if (!Directory.Exists(_latestVersionDirectory))
                _ = Frontend.ShowMessageBox(Strings.Bootstrapper_Dialog_NoUpgradeWithoutClient, MessageBoxImage.Warning, MessageBoxButton.OK);
            else
            {
                await Task.Delay(2000);
                return;
            }
        }

        SetStatus(string.IsNullOrEmpty(AppData.DistributionState.VersionGuid)
            ? Strings.Bootstrapper_Status_Installing
            : Strings.Bootstrapper_Status_Upgrading);

        Directory.CreateDirectory(Paths.Base);
        Directory.CreateDirectory(Paths.Downloads);
        Directory.CreateDirectory(Paths.Versions);

        _isInstalling = true;

        // make sure nothing is running before continuing upgrade
        if (!App.LaunchSettings.BackgroundUpdaterFlag.Active && !IsStudioLaunch)
            KillRobloxPlayers();

        // get a fully clean install
        if (!App.LaunchSettings.BackgroundUpdaterFlag.Active && Directory.Exists(_latestVersionDirectory))
        {
            try
            {
                Directory.Delete(_latestVersionDirectory, true);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to delete the latest version directory");
            }
        }

        Directory.CreateDirectory(_latestVersionDirectory);

        if (OperatingSystem.IsMacOS())
        {
            string backupDir = GetResourcesBackupPath(_latestVersionGuid);
            if (!Directory.Exists(backupDir))
                return;
            try
            {
                Directory.Delete(backupDir, true);
                App.Logger.Info($"Deleted existing mod backup for {_latestVersionGuid}");
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Failed to delete mod backup: {ex.Message}");
            }
        }

        var cachedPackageHashes = Directory.GetFiles(Paths.Downloads).Select(x => Path.GetFileName(x)).ToList();

        // package manifest states packed size and uncompressed size in exact bytes
        long totalSizeRequired = 0;

        // packed size only matters if we don't already have the package cached on disk
        var installPackages = _versionPackageManifest.Where(p => p.Name != "RobloxPlayerInstaller.exe").ToList();

        totalSizeRequired += installPackages.Where(x => !cachedPackageHashes.Contains(x.Signature)).Sum(x => (long)x.PackedSize);
        totalSizeRequired += installPackages.Sum(x => (long)x.Size);

        long freeDiskSpace = Filesystem.GetFreeDiskSpace(Paths.Base);

        App.Logger.Info($"Disk space check for '{Paths.Base}': {freeDiskSpace} bytes free, {totalSizeRequired} bytes required");

        if (freeDiskSpace < 0)
            App.Logger.Warn("Could not determine free disk space, skipping the disk space check.");
        else if (freeDiskSpace < totalSizeRequired)
        {
            await Frontend.ShowMessageBox(Strings.Bootstrapper_NotEnoughSpace, MessageBoxImage.Error);
            App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
            return;
        }

        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = false;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;

            Dialog.ProgressMaximum = ProgressBarMaximum;

            // compute total bytes to download
            _totalPackagedBytes = _versionPackageManifest.Where(p => p.Name != "RobloxPlayerInstaller.exe").Sum(package => package.PackedSize);
            _progressIncrement = (double)ProgressBarMaximum / _totalPackagedBytes;

            _taskbarProgressMaximum = TaskbarProgressMaximum;

            _taskbarProgressIncrement = _taskbarProgressMaximum / (double)_totalPackagedBytes;
        }

        var packageTasks = new List<Task>();

        // from largest to smallest, this is so larger packages (which need more time) get queued first
        var packages = _versionPackageManifest.Where(p => p.Name != "RobloxPlayerInstaller.exe").OrderByDescending(p => p.PackedSize);

        using var downloadSemaphore = new SemaphoreSlim(App.Settings.Prop.MaxThreadDownload > 0 ? App.Settings.Prop.MaxThreadDownload : 1);
        foreach (var package in packages)
        {
            await downloadSemaphore.WaitAsync(_cancelTokenSource.Token);


            var task = Task.Run(async () =>
            {
                try
                {
                    await DownloadPackage(package);

                    // we'll extract the runtime installer later if we need to
                    if (package.Name != "WebView2RuntimeInstaller.zip")
                        await ExtractPackage(package);
                }
                finally
                {
                    downloadSemaphore.Release();
                }
            }, _cancelTokenSource.Token);

            packageTasks.Add(task);
        }
        await Task.WhenAll(packageTasks);

        if (_cancelTokenSource.IsCancellationRequested) return;

        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = true;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
            SetStatus(Strings.Bootstrapper_Status_Configuring);
        }

        if (OperatingSystem.IsWindows() && App.State.Prop.PromptWebView2Install)
        {
            using var hklmKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
            using var hkcuKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");

            // reset prompt state if the user has it installed
            if (hklmKey is not null || hkcuKey is not null)
                App.State.Prop.PromptWebView2Install = true;
            else
            {
                var result = await Frontend.ShowMessageBox(Strings.Bootstrapper_WebView2NotFound, MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.Yes);

                if (result != MessageBoxResult.Yes)
                    App.State.Prop.PromptWebView2Install = false;
                else
                {
                    App.Logger.Info("Installing WebView2 runtime...");

                    var package = _versionPackageManifest.Find(x => x.Name == "WebView2RuntimeInstaller.zip");

                    if (package is null)
                    {
                        App.Logger.Info("Aborted runtime install because package does not exist, has WebView2 been added in this Roblox version yet?");
                        return;
                    }

                    string baseDirectory = Path.Combine(_latestVersionDirectory, PackageDirectoryMap[package.Name]);

                    await ExtractPackage(package);

                    SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);

                    var startInfo = new ProcessStartInfo()
                    {
                        WorkingDirectory = baseDirectory,
                        FileName = Path.Combine(baseDirectory, "MicrosoftEdgeWebview2Setup.exe"),
                        Arguments = "/silent /install"
                    };

                    await Process.Start(startInfo)!.WaitForExitAsync();

                    App.Logger.Info("Finished installing runtime");

                    Directory.Delete(baseDirectory, true);
                }
            }
        }

        if (OperatingSystem.IsMacOS())
        {
            string[] appNames = ["RobloxPlayer.app", "RobloxStudio.app"];
            foreach (string appName in appNames)
            {
                string appPath = Path.Combine(_latestVersionDirectory, appName);
                if (!Directory.Exists(appPath)) continue;

                string macOSDir = Path.Combine(appPath, "Contents", "MacOS");
                if (Directory.Exists(macOSDir)) foreach (string file in Directory.GetFiles(macOSDir))
                {
                    var fileInfo = new FileInfo(file);
                    fileInfo.UnixFileMode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                }
                using var process = Process.Start("xattr", $"-dr com.apple.quarantine \"{appPath}\"");
                await process!.WaitForExitAsync();
            }
        }

        // finishing and cleanup
        MigrateCompatibilityFlags();

        AppData.DistributionState.VersionGuid = _latestVersionGuid;

        AppData.DistributionState.PackageHashes.Clear();

        foreach (var package in _versionPackageManifest)
            AppData.DistributionState.PackageHashes.Add(package.Name, package.Signature);

        CleanupVersionsFolder();

        var allPackageHashes = new List<string>();

        allPackageHashes.AddRange(App.PlayerState.Prop.PackageHashes.Values);
        allPackageHashes.AddRange(App.StudioState.Prop.PackageHashes.Values);

        if (!App.Settings.Prop.DebugDisableVersionPackageCleanup) foreach (string hash in cachedPackageHashes)
        {
            if (allPackageHashes.Contains(hash))
                return;

            App.Logger.Info($"Deleting unused package {hash}");

            try
            {
                File.Delete(Path.Combine(Paths.Downloads, hash));
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"Failed to delete {hash}!");
            }
        }

        App.State.Prop.ForceReinstall = false;
        App.State.Save();
        AppData.DistributionStateManager.Save();

        if (OperatingSystem.IsWindows())
            WindowsRegistry.UpdateEstimatedSize();

        if (!IsStudioLaunch) InitializeModFolders();
        _isInstalling = false;
    }

    private async Task DownloadPackage(Package package, bool updateProgress = true)
    {
        if (_cancelTokenSource.IsCancellationRequested)
            return;

        Directory.CreateDirectory(Paths.Downloads);

        string packageUrl = OperatingSystem.IsMacOS()
            ? Deployment.GetLocation($"{GetMacArchPath()}/{_latestVersionGuid}-{package.Name}")
            : Deployment.GetLocation($"/{_latestVersionGuid}-{package.Name}");
        string robloxPackageLocation = Path.Combine(Paths.LocalAppData, "Roblox", "Downloads", package.Signature);

        if (File.Exists(package.DownloadPath))
        {
            string calculatedFastHash = FastHash.FromFile(package.DownloadPath);

            if (OperatingSystem.IsMacOS())
            {
                var knownState = IsStudioLaunch ? App.StudioState : App.PlayerState;

                if (knownState.Prop.PackageHashes.TryGetValue(package.Name, out string? knownHash))
                {
                    if (calculatedFastHash != knownHash)
                    {
                        App.Logger.Warn($"Cached package is corrupted ({calculatedFastHash} != {knownHash})! Deleting and re-downloading...");
                        File.Delete(package.DownloadPath);
                    }
                    else
                    {
                        App.Logger.Info("Package is already downloaded and matches its recorded hash, skipping...");
                        if (updateProgress)
                        {
                            Interlocked.Add(ref _totalDownloadedBytes, package.PackedSize);
                            UpdateProgressBar();
                        }
                        return;
                    }
                }
                else
                {
                    App.Logger.Warn($"No recorded hash for {package.Name}; anchoring integrity to existing file ({calculatedFastHash})");
                    knownState.Prop.PackageHashes[package.Name] = calculatedFastHash;
                    knownState.Save();

                    App.Logger.Info("Package is already downloaded, skipping...");
                    if (updateProgress)
                    {
                        Interlocked.Add(ref _totalDownloadedBytes, package.PackedSize);
                        UpdateProgressBar();
                    }
                    return;
                }
            }
            else if (calculatedFastHash != package.Signature)
            {
                App.Logger.Warn($"Package is corrupted ({calculatedFastHash} != {package.Signature})! Deleting and re-downloading...");
                File.Delete(package.DownloadPath);
            }
            else
            {
                App.Logger.Info("Package is already downloaded, skipping...");
                if (updateProgress)
                {
                    Interlocked.Add(ref _totalDownloadedBytes, package.PackedSize);
                    UpdateProgressBar();
                }
                return;
            }
        }
        else if (File.Exists(robloxPackageLocation))
        {
            // let's cheat! if the stock bootstrapper already previously downloaded the file,
            // then we can just copy the one from there

            App.Logger.Info($"Found existing copy at '{robloxPackageLocation}'! Copying to Downloads folder...");
            File.Copy(robloxPackageLocation, package.DownloadPath);
            if (updateProgress)
            {
                Interlocked.Add(ref _totalDownloadedBytes, package.PackedSize);
                UpdateProgressBar();
            }
            return;
        }

        App.Logger.Info("Downloading...");

        var buffer = new byte[DownloadBufferSize];

        for (int i = 1; i <= MaxDownloadAttempts; i++)
        {
            if (_cancelTokenSource.IsCancellationRequested)
                return;

            int totalBytesRead = 0;

            try
            {
                using var response = await App.HttpClient.GetAsync(new Uri(packageUrl), HttpCompletionOption.ResponseHeadersRead, _cancelTokenSource.Token);
                response.EnsureSuccessStatusCode();

                if (updateProgress
                    && package.PackedSize <= 0
                    && response.Content.Headers.ContentLength is long contentLength and > 0
                    && _sizedFromResponse.TryAdd(package.Name, 0))
                {
                    AddProgressTotal(contentLength);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(_cancelTokenSource.Token);
                await using var fileStream = new FileStream(package.DownloadPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Delete);
                while (true)
                {
                    if (_cancelTokenSource.IsCancellationRequested)
                    {
                        stream.Close();
                        fileStream.Close();
                        return;
                    }

                    int bytesRead = await stream.ReadAsync(buffer, _cancelTokenSource.Token);

                    if (bytesRead == 0)
                        break;

                    totalBytesRead += bytesRead;

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancelTokenSource.Token);

                    if (updateProgress)
                    {
                        Interlocked.Add(ref _totalDownloadedBytes, bytesRead);
                        UpdateProgressBar();
                    }
                }
                await fileStream.FlushAsync();
                fileStream.Position = 0;

                string hash = FastHash.FromStream(fileStream);

                if (OperatingSystem.IsMacOS())
                {
                    var knownState = IsStudioLaunch ? App.StudioState : App.PlayerState;
                    knownState.Prop.PackageHashes[package.Name] = hash;
                    knownState.Save();
                }
                else if (!string.Equals(hash, package.Signature, StringComparison.OrdinalIgnoreCase))
                    throw new ChecksumFailedException($"Failed to verify download of {packageUrl}\n\nExpected hash: {package.Signature}\nGot hash: {hash}");

                App.Logger.Info($"Finished downloading! ({totalBytesRead} bytes total)");
                break;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, $"An exception occurred after downloading {totalBytesRead} bytes. ({i}/{MaxDownloadAttempts})");

                if (ex is ChecksumFailedException)
                {
                    await Frontend.ShowConnectivityDialog(
                        Strings.Dialog_Connectivity_UnableToDownload,
                        String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Connectivity_UnableToDownloadReason, "[https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox](https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox)"),
                        MessageBoxImage.Error,
                        ex
                    );

                    App.Terminate(ErrorCode.ERROR_CANCELLED);
                }
                else if (i >= MaxDownloadAttempts)
                    throw;

                if (File.Exists(package.DownloadPath))
                    File.Delete(package.DownloadPath);

                if (updateProgress)
                {
                    Interlocked.Add(ref _totalDownloadedBytes, -totalBytesRead);
                    UpdateProgressBar();
                }

                // attempt download over HTTP
                // SAFETY:
                //   signatures were fetched earlier over HTTPS
                //   on macOS there is no signature to verify, so the download
                //   must never leave HTTPS (see DownloadPackage's hash check)
                if (ex is IOException
                    && !OperatingSystem.IsMacOS()
                    && !packageUrl.StartsWith("http://", StringComparison.Ordinal))
                {
                    App.Logger.Info("Retrying download over HTTP...");
                    packageUrl = packageUrl.Replace("https://", "http://", StringComparison.Ordinal);
                }
            }
        }
    }

    private void ExtractZipBcl(string zipPath, string targetFolder, string? fileFilter)
    {
        Regex[]? compiledFilters = null;
        if (!string.IsNullOrEmpty(fileFilter))
            compiledFilters = [.. fileFilter.Split(';').Select(p => new Regex(p))];

        string fullTargetFolder = Path.GetFullPath(targetFolder);
        if (!fullTargetFolder.EndsWith(Path.DirectorySeparatorChar))
            fullTargetFolder += Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zipPath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (_cancelTokenSource.IsCancellationRequested)
                return;

            if (entry.FullName.EndsWith('/'))
                continue;

            string entryName = entry.FullName.Replace('\\', '/');

            if (compiledFilters is not null && !compiledFilters.Any(r => r.IsMatch(entryName)))
                continue;

            string targetPath = Path.GetFullPath(Path.Combine(fullTargetFolder, entryName));

            if (!targetPath.StartsWith(fullTargetFolder, StringComparison.Ordinal))
            {
                App.Logger.Warn($"Skipping entry with path-traversal pattern: {entry.FullName}");
                continue;
            }

            string? targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            try
            {
                entry.ExtractToFile(targetPath, overwrite: true);
            }
            catch (Exception ex) when (entry.Name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.Error($"Failed to extract {entry.FullName}: {ex.Message}");
                _packageExtractionSuccess = false;
            }
        }
    }

    private async Task<bool> ExtractPackage(Package package, List<string>? files = null)
    {
        int attempts = 0;
        const int maxAttempts = 3;

        while (attempts < maxAttempts)
        {
            try
            {
                attempts++;

                string? packageDir = PackageDirectoryMap.GetValueOrDefault(package.Name);
                if (packageDir is null)
                {
                    App.Logger.Warn($"WARNING: {package.Name} not found in package map, skipping.");
                    return true;
                }

                string targetFolder = Path.Combine(_latestVersionDirectory, packageDir);
                Directory.CreateDirectory(targetFolder);

                if (files != null && files.Count > 0)
                {
                    foreach (string relativePath in files)
                    {
                        string fullPath = Path.Combine(targetFolder, relativePath);
                        if (File.Exists(fullPath))
                        {
                            try { File.SetAttributes(fullPath, FileAttributes.Normal); File.Delete(fullPath); }
                            catch (Exception ex) { App.Logger.Error($"Failed to delete {fullPath}: {ex.Message}"); }
                        }
                    }
                }

                string? fileFilter = null;
                if (files != null && files.Count > 0)
                {
                    var regexList = new List<string>();
                    foreach (string file in files)
                        regexList.Add("^" + Regex.Escape(file) + "$");
                    fileFilter = string.Join(';', regexList);
                }

                App.Logger.Info($"Extracting {package.Name} (Attempt {attempts}/{maxAttempts})...");

                if (OperatingSystem.IsLinux() && IsStudioLaunch)
                {
                    await ExtractZipLinux(package.DownloadPath, targetFolder, fileFilter, _cancelTokenSource.Token);
                }
                else
                {
                    ExtractZipBcl(package.DownloadPath, targetFolder, fileFilter);
                }

                App.Logger.Info($"Finished extracting {package.Name}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Extraction failed on attempt {attempts}: {ex.Message}");

                if (ex.Message.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                {
                    App.Logger.Warn($"Ignoring non‑critical extraction failure for font file.");
                    return true;
                }

                if (File.Exists(package.DownloadPath))
                {
                    App.Logger.Info("Deleting corrupted package for retry...");
                    File.Delete(package.DownloadPath);
                }

                string? retryDir = PackageDirectoryMap.GetValueOrDefault(package.Name);
                if (files is null && !string.IsNullOrEmpty(retryDir))
                {
                    string retryTargetFolder = Path.Combine(_latestVersionDirectory, retryDir);
                    try
                    {
                        if (Directory.Exists(retryTargetFolder))
                            Directory.Delete(retryTargetFolder, true);
                    }
                    catch (Exception cleanupEx)
                    {
                        App.Logger.Error($"Failed to clean up partial extraction: {cleanupEx.Message}");
                    }
                }

                if (attempts >= maxAttempts)
                {
                    App.Logger.Error($"Max extraction attempts reached for {package.Name}. Aborting install.");
                    throw new InvalidOperationException($"Failed to extract package {package.Name} after {maxAttempts} attempts.", ex);
                }

                App.Logger.Info("Retrying download...");
                SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_RetryingPackage, package.Name));
                await Task.Delay(1000);

                Interlocked.Add(ref _totalDownloadedBytes, -(long)package.PackedSize);
                await DownloadPackage(package);
            }
        }

        return false;
    }

    private static async Task ExtractZipLinux(string zipPath, string targetFolder, string? fileFilter, CancellationToken cancellationToken)
    {
        Regex[]? compiledFilters = null;
        if (!string.IsNullOrEmpty(fileFilter))
            compiledFilters = [.. fileFilter.Split(';').Select(p => new Regex(p))];

        await Task.Run(async () =>
        {
            using var archive = await ZipFile.OpenReadAsync(zipPath, cancellationToken);

            string fullTargetFolder = Path.GetFullPath(targetFolder);
            if (!fullTargetFolder.EndsWith(Path.DirectorySeparatorChar))
                fullTargetFolder += Path.DirectorySeparatorChar;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/'))
                    continue;

                string entryName = entry.FullName.Replace('\\', '/');

                if (compiledFilters is not null && !compiledFilters.Any(r => r.IsMatch(entryName)))
                    continue;

                string targetPath = Path.GetFullPath(Path.Combine(fullTargetFolder, entryName));

                if (!targetPath.StartsWith(fullTargetFolder, StringComparison.Ordinal))
                    continue;

                string? targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir))
                    Directory.CreateDirectory(targetDir);

                await using var stream = await entry.OpenAsync(cancellationToken);
                using var fileStream = File.Create(targetPath);
                await stream.CopyToAsync(fileStream, cancellationToken);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Will throw whatever HttpClient can throw
    /// </summary>
    /// <returns></returns>
    private async Task GetLatestVersionInfo()
    {

        // before we do anything, we need to query our channel
        // if it's set in the launch uri, we need to use it and set the registry key for it
        // else, check if the registry key for it exists, and use it

        var match = Regex.Match(
            App.LaunchSettings.RobloxLaunchArgs,
            "channel:([a-zA-Z0-9-_]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        void EnrollChannel(string channel = "production")
        {
            Deployment.Channel = channel;
            if (IsStudioLaunch)
                App.Settings.Prop.StudioChannel = channel;
            else
                App.Settings.Prop.PlayerChannel = channel;
            App.Settings.Save();
        }

        void RevertChannel()
        {
            Deployment.Channel = Deployment.DefaultChannel;
            Deployment.ChannelToken = string.Empty;
            if (IsStudioLaunch)
                App.Settings.Prop.StudioChannel = Deployment.DefaultChannel;
            else
                App.Settings.Prop.PlayerChannel = Deployment.DefaultChannel;
            App.Settings.Save();
        }

#pragma warning disable CA1308
        string enrolledChannel = match.Groups.Count == 2
            ? match.Groups[1].Value.ToLowerInvariant()
            : Deployment.DefaultChannel;
#pragma warning restore CA1308

        bool behindProductionCheck = App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt;

        // Private channels
        string? tokenChannel = null;

        if (App.Cookies.Loaded)
        {
            UserChannel? userChannel = await Deployment.GetUserChannel(AppData.BinaryType);

            if (
                userChannel?.Token is not null &&
                userChannel.AssignmentType != 1 // might need a change in future
            )
            {
                // prevent roblox from thinking its different channel
                // have to do to prevent issues with channel fflags
                if (!string.IsNullOrEmpty(enrolledChannel))
                    _launchCommandLine = _launchCommandLine.Replace(
                        $"channel:{enrolledChannel}",
                        $"channel:{userChannel.Channel}",
                        StringComparison.OrdinalIgnoreCase);

                Deployment.ChannelToken = userChannel.Token;
                tokenChannel = userChannel.Channel;
                enrolledChannel = userChannel.Channel;
            }
        }

        bool channelFlag = App.LaunchSettings.ChannelFlag.Active && !string.IsNullOrEmpty(App.LaunchSettings.ChannelFlag.Data);

        if (!channelFlag)
        {
            switch (App.Settings.Prop.ChannelChangeMode)
            {
                case ChannelChangeMode.Automatic:
                    App.Logger.Info("Enrolling into channel");
                    EnrollChannel(enrolledChannel);
                    break;

                case ChannelChangeMode.Prompt:
                    App.Logger.Debug("Prompting channel enrollment");

                    if (!match.Success || match.Groups.Count != 2 || string.Equals(match.Groups[1].Value, Deployment.Channel, StringComparison.OrdinalIgnoreCase))
                    {
                        App.Logger.Warn("Channel is either equal or incorrectly formatted");
                        break;
                    }

                    string displayChannel = !String.IsNullOrEmpty(match.Groups[1].Value)
                        ? match.Groups[1].Value
                        : Deployment.DefaultChannel;

                    var promptResult = await Frontend.ShowMessageBox(
                        String.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Bootstrapper_Dialog_PromptChannelChange, displayChannel, Deployment.Channel),
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                    );

                    if (promptResult == MessageBoxResult.Yes)
                        EnrollChannel(enrolledChannel);
                    break;

                case ChannelChangeMode.Ignore:
                    App.Logger.Debug("Ignoring channel enrollment");
                    break;
            }
        }
        else
        {
            string channelFlagData = App.LaunchSettings.ChannelFlag.Data!;
            if (String.IsNullOrEmpty(channelFlagData))
                return;

            App.Logger.Debug($"Forcing channel {channelFlagData}");
            EnrollChannel(channelFlagData);
        }

        if (!string.IsNullOrEmpty(Deployment.ChannelToken)
            && !string.Equals(Deployment.Channel, tokenChannel, StringComparison.OrdinalIgnoreCase))
        {
            App.Logger.Info($"Dropping channel token for '{tokenChannel}' because the active channel is '{Deployment.Channel}'");
            Deployment.ChannelToken = string.Empty;
        }

        bool overrideUsed = false;

        if (IsStudioLaunch && App.Settings.Prop.StudioVersionOverrideEnabled && !string.IsNullOrEmpty(App.Settings.Prop.StudioVersionOverrideHash))
        {
            _latestVersionGuid = App.Settings.Prop.StudioVersionOverrideHash.Trim();
            App.Logger.Debug($"Studio version override active: pinned to {_latestVersionGuid}");
            overrideUsed = true;
        }
        else if (!IsStudioLaunch && App.Settings.Prop.PlayerVersionOverrideEnabled && !string.IsNullOrEmpty(App.Settings.Prop.PlayerVersionOverrideHash))
        {
            string overrideHash = App.Settings.Prop.PlayerVersionOverrideHash.Trim();
            var (valid, error) = await UI.ViewModels.Settings.ChannelViewModel.ValidateHashCore(overrideHash, true, AppData.BinaryType);

            if (valid)
            {
                _latestVersionGuid = overrideHash;
                App.Logger.Debug($"Player version override active: pinned to {_latestVersionGuid}");
                overrideUsed = true;
            }
            else
            {
                App.Logger.Warn($"Player version override invalid: {error}. Falling back to channel.");

                await Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_InvalidOverride, overrideHash, error),
                    MessageBoxImage.Warning,
                    MessageBoxButton.OK
                );

                App.Settings.Prop.PlayerVersionOverrideEnabled = false;
                App.Settings.Prop.PlayerVersionOverrideHash = string.Empty;
                App.Settings.Save();
            }
        }

        if (!overrideUsed && (!App.LaunchSettings.VersionFlag.Active || string.IsNullOrEmpty(App.LaunchSettings.VersionFlag.Data)))
        {
            ClientVersion clientVersion;

            try
            {
                clientVersion = await Deployment.GetInfo(Deployment.Channel, behindProductionCheck, false, AppData.BinaryType);
            }
            catch (InvalidChannelException ex)
            {
                // If channel does not exist
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    App.Logger.Warn($"Reverting enrolled channel to {Deployment.DefaultChannel} because a WindowsPlayer build does not exist for {Deployment.Channel}");
                }
                // If channel is not available to the user (private/internal release channel)
                else if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    App.Logger.Warn($"Reverting enrolled channel to {Deployment.DefaultChannel} because {Deployment.Channel} is restricted for public use.");

                    // Only prompt if user has channel switching mode set to something other than Automatic.
                    if (App.Settings.Prop.ChannelChangeMode != ChannelChangeMode.Automatic)
                    {
                        await Frontend.ShowMessageBox(
                            String.Format(CultureInfo.InvariantCulture,
                                Strings.Boostrapper_Dialog_UnauthorizedChannel,
                                Deployment.Channel,
                                Deployment.DefaultChannel
                            ),
                            MessageBoxImage.Information
                        );
                    }
                }
                else
                {
                    throw;
                }

                RevertChannel();
                clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, behindProductionCheck, false, AppData.BinaryType);
            }

            if (clientVersion.IsBehindDefaultChannel && App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt)
            {
                MessageBoxResult action = await Frontend.ShowMessageBox(
                        String.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Dialog_ChannelOutOfDate, Deployment.Channel, Deployment.DefaultChannel),
                        MessageBoxImage.Warning,
                        MessageBoxButton.YesNo
                    );

                if (action == MessageBoxResult.Yes)
                {
                    App.Logger.Debug($"Changed Roblox channel from {Deployment.Channel} to {Deployment.DefaultChannel}");

                    RevertChannel();
                    clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, behindProductionCheck: false, binaryTypeOverride: AppData.BinaryType);
                }
            }

            _latestVersionGuid = clientVersion.VersionGuid;
            _latestVersion = Utility.Versioning.ParseVersionSafe(clientVersion.Version);
        }
        else if (!overrideUsed)
        {
            string? versionData = App.LaunchSettings.VersionFlag.Data;
            if (string.IsNullOrEmpty(versionData))
            {
                App.Logger.Error("VersionFlag.Data was unexpectedly null or empty. Falling back to default channel.");
                var fallbackInfo = await Deployment.GetInfo(Deployment.DefaultChannel, false, false, AppData.BinaryType);
                _latestVersionGuid = fallbackInfo.VersionGuid;
                _latestVersion = Utility.Versioning.ParseVersionSafe(fallbackInfo.Version);
            }
            else
            {
                App.Logger.Debug($"Version set to {versionData} from arguments");
                _latestVersionGuid = versionData;
            }
        }

        SyncLaunchChannel();

        if (App.Settings.Prop.StaticDirectory)
            _latestVersionDirectory = AppData.StaticDirectory;
        else
            _latestVersionDirectory = Path.Combine(Paths.Versions, _latestVersionGuid);

        // Mods are applied directly into Sober's asset_overlay directory instead of a versioned folder.
        if (OperatingSystem.IsLinux() && !IsStudioLaunch)
            _latestVersionDirectory = Paths.SoberAssetOverlay;

        if (OperatingSystem.IsMacOS())
        {
            // Mac uses monolithic zip downloads instead of individual packages
            string zipName = IsStudioLaunch ? "RobloxStudioApp.zip" : "RobloxPlayer.zip";

            // Construct a fake package manifest response to trick the internal system
            string fakeManifest = $"v0\n{zipName}\n{_latestVersionGuid}\n0\n0";
            _versionPackageManifest = new(fakeManifest);
        }
        else
        {
            string pkgManifestUrl = Deployment.GetLocation($"/{_latestVersionGuid}-rbxPkgManifest.txt");
            var pkgManifestData = await App.HttpClient.GetStringAsync(new Uri(pkgManifestUrl));
            _versionPackageManifest = new(pkgManifestData);
        }

        // this can happen if version is set through arguments
        if (_launchMode == LaunchMode.Unknown)
        {
            App.Logger.Info("Identifying launch mode from package manifest");

            bool isPlayer = _versionPackageManifest.Exists(x => x.Name == "RobloxApp.zip" || x.Name == "RobloxPlayer.zip");
            App.Logger.Info($"isPlayer={isPlayer}");

            _launchMode = isPlayer ? LaunchMode.Player : LaunchMode.Studio;
            SetupAppData(); // we need to set it up again
        }
    }
}
