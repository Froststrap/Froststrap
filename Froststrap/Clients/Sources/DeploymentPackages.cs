// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;
using System.IO.Compression;
using Froststrap.Clients.Contracts;
using Froststrap.Models.Manifest;

namespace Froststrap.Clients.Sources;

internal sealed class DeploymentPackages(
    IRobloxClient client,
    JsonManager<DistributionState> stateManager,
    IPackageReporter reporter,
    ConcurrentDictionary<string, byte> sizedFromResponse,
    string versionGuid,
    string versionDirectory,
    IReadOnlyDictionary<string, string> packageDirectoryMap,
    CancellationToken cancellationToken)
{
    private const int DownloadBufferSize = 4096;
    private const int MaxDownloadAttempts = 5;

    private readonly DeploymentCdnSource _source = new(client.Layout);

    public async Task DownloadAsync(Package package, bool updateProgress = true)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        Directory.CreateDirectory(Paths.Downloads);

        bool signedPackages = _source.HasPackageSignatures;
        string packageUrl = _source.GetPackageUrl(versionGuid, package.Name);
        string robloxPackageLocation = Path.Combine(Paths.LocalAppData, "Roblox", "Downloads", package.Signature);

        if (File.Exists(package.DownloadPath))
        {
            string calculatedFastHash = FastHash.FromFile(package.DownloadPath);

            if (!signedPackages)
            {
                var knownState = stateManager;

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
                            reporter.AddDownloaded(package.PackedSize);
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
                        reporter.AddDownloaded(package.PackedSize);
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
                    reporter.AddDownloaded(package.PackedSize);
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
                reporter.AddDownloaded(package.PackedSize);
            }
            return;
        }

        App.Logger.Info("Downloading...");

        var buffer = new byte[DownloadBufferSize];

        for (int i = 1; i <= MaxDownloadAttempts; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            int totalBytesRead = 0;

            try
            {
                using var response = await App.HttpClient.GetAsync(new Uri(packageUrl), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                if (updateProgress
                    && package.PackedSize <= 0
                    && response.Content.Headers.ContentLength is long contentLength and > 0
                    && sizedFromResponse.TryAdd(package.Name, 0))
                {
                    reporter.AddTotal(contentLength);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(package.DownloadPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Delete);
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        stream.Close();
                        fileStream.Close();
                        return;
                    }

                    int bytesRead = await stream.ReadAsync(buffer, cancellationToken);

                    if (bytesRead == 0)
                        break;

                    totalBytesRead += bytesRead;

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                    if (updateProgress)
                    {
                        reporter.AddDownloaded(bytesRead);
                    }
                }
                await fileStream.FlushAsync();
                fileStream.Position = 0;

                string hash = FastHash.FromStream(fileStream);

                if (!signedPackages)
                {
                    var knownState = stateManager;
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
                    await reporter.OnChecksumFailedAsync(ex);
                }
                else if (i >= MaxDownloadAttempts)
                    throw;

                if (File.Exists(package.DownloadPath))
                    File.Delete(package.DownloadPath);

                if (updateProgress)
                {
                    reporter.AddDownloaded(-totalBytesRead);
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
            if (cancellationToken.IsCancellationRequested)
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

            if (!OperatingSystem.IsWindows() && IsSymbolicLink(entry))
            {
                ExtractSymbolicLink(entry, targetPath, fullTargetFolder);
                continue;
            }

            try
            {
                entry.ExtractToFile(targetPath, overwrite: true);
            }
            catch (Exception ex) when (entry.Name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.Error($"Failed to extract {entry.FullName}: {ex.Message}");
                reporter.MarkExtractionFailed();
            }
        }
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        const int fileTypeMask = 0xF000;
        const int symbolicLinkType = 0xA000;
        return ((entry.ExternalAttributes >> 16) & fileTypeMask) == symbolicLinkType;
    }

    private static void ExtractSymbolicLink(ZipArchiveEntry entry, string targetPath, string fullTargetFolder)
    {
        string linkTarget;
        using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
            linkTarget = reader.ReadToEnd();

        string resolvedTarget = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(targetPath)!, linkTarget));
        if (Path.IsPathRooted(linkTarget) || !resolvedTarget.StartsWith(fullTargetFolder, StringComparison.Ordinal))
        {
            App.Logger.Warn($"Skipping symbolic link pointing outside the extraction folder: {entry.FullName} -> {linkTarget}");
            return;
        }

        if (File.Exists(targetPath) || new FileInfo(targetPath).LinkTarget is not null)
            File.Delete(targetPath);

        File.CreateSymbolicLink(targetPath, linkTarget);
    }

    public async Task<bool> ExtractAsync(Package package, List<string>? files = null)
    {
        int attempts = 0;
        const int maxAttempts = 3;

        while (attempts < maxAttempts)
        {
            try
            {
                attempts++;

                string? packageDir = packageDirectoryMap.GetValueOrDefault(package.Name);
                if (packageDir is null)
                {
                    App.Logger.Warn($"WARNING: {package.Name} not found in package map, skipping.");
                    return true;
                }

                string targetFolder = Path.Combine(versionDirectory, packageDir);
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

                if (client.RuntimeEnvironment is not null)
                {
                    await ExtractZipLinux(package.DownloadPath, targetFolder, fileFilter, cancellationToken);
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

                string? retryDir = packageDirectoryMap.GetValueOrDefault(package.Name);
                if (files is null && !string.IsNullOrEmpty(retryDir))
                {
                    string retryTargetFolder = Path.Combine(versionDirectory, retryDir);
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
                reporter.SetStatus(string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_RetryingPackage, package.Name));
                await Task.Delay(1000);

                reporter.AddDownloaded(-(long)package.PackedSize, updateProgressBar: false);
                await DownloadAsync(package);
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
}
