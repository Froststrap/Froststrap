// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap;

internal sealed class RosemaryManager(HttpClient httpClient)
{
    private const string ReleaseUrl = "https://api.github.com/repos/microlift/rosemary/releases/latest";
    private const string BinaryName = "rosemary";
    private static readonly TimeSpan ReleaseCheckTimeout = TimeSpan.FromSeconds(10);

    public static string? FindExisting()
    {
        string managed = App.RosemaryData.ExecutablePath;

        if (File.Exists(managed))
        {
            if (!IsExecutable(managed))
            {
                try
                {
                    MarkExecutable(managed);
                }
                catch (Exception ex)
                {
                    App.Logger.Warn($"Could not make Rosemary executable: {ex.Message}");
                }
            }

            return managed;
        }

        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, BinaryName);
            if (IsExecutable(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    public async Task<string?> EnsureAsync(Func<long, long, Task>? reportProgress = null, CancellationToken cancellationToken = default)
    {
        MigrateLegacyInstall();

        string managed = App.RosemaryData.ExecutablePath;
        string? existing = FindExisting();

        if (existing is not null && !string.Equals(existing, managed, StringComparison.Ordinal))
        {
            App.Logger.Info($"Using Rosemary from PATH: {existing}");
            return existing;
        }

        GithubRelease? release;
        using (var releaseCheck = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            releaseCheck.CancelAfter(ReleaseCheckTimeout);

            try
            {
                release = await GetLatestReleaseAsync(releaseCheck.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                App.Logger.Warn("Timed out checking for Rosemary updates.");
                return existing;
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not check for Rosemary updates: {ex.Message}");
                return existing;
            }
        }

        if (release is null)
            return existing;

        string? downloadUrl = GetAppImageUrl(release);
        if (downloadUrl is null)
        {
            App.Logger.Warn($"The latest Rosemary release ({release.TagName}) has no AppImage asset.");
            return existing;
        }

        string? installedVersion = existing is null ? null : App.State.Prop.RosemaryVersion;

        if (existing is not null && !IsUpdateAvailable(release.TagName, installedVersion))
        {
            App.Logger.Info($"Rosemary {installedVersion} is up to date.");
            return existing;
        }

        return await DownloadAsync(release, downloadUrl, reportProgress, cancellationToken) ?? existing;
    }

    private async Task<GithubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        return await JsonHelper.GetFromJsonAsync<GithubRelease>(httpClient, ReleaseUrl, cancellationToken)
            ?? throw new InvalidOperationException("Could not read the latest Rosemary release.");
    }

    private static string? GetAppImageUrl(GithubRelease release)
    {
        return release.Assets?
            .Select(asset => asset.BrowserDownloadUrl)
            .FirstOrDefault(url => url?.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool IsUpdateAvailable(string latestVersion, string? installedVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
            return true;

        if (string.Equals(latestVersion, installedVersion, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            return Versioning.CompareVersions(latestVersion, installedVersion) == VersionComparison.GreaterThan;
        }
        catch (Exception ex)
        {
            App.Logger.Warn($"Could not compare Rosemary versions ({installedVersion} -> {latestVersion}): {ex.Message}");
            return true;
        }
    }

    private async Task<string?> DownloadAsync(GithubRelease release, string downloadUrl, Func<long, long, Task>? reportProgress, CancellationToken cancellationToken)
    {
        string destination = App.RosemaryData.ExecutablePath;
        string temporary = destination + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using var response = await httpClient.GetAsync(new Uri(downloadUrl), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? 0;
            long current = 0;

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    current += read;

                    if (reportProgress is not null)
                        await reportProgress(current, total);
                }
            }

            if (current == 0 || (total > 0 && current != total))
                throw new IOException($"Downloaded Rosemary AppImage is incomplete ({current} of {total} bytes).");

            MarkExecutable(temporary);
            File.Move(temporary, destination, true);

            App.State.Prop.RosemaryVersion = release.TagName;
            App.State.Save();

            App.Logger.Info($"Installed Rosemary {release.TagName} to {destination}");
            return destination;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to download Rosemary.");
            return null;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception ex)
                {
                    App.Logger.Warn($"Could not remove temporary Rosemary file: {ex.Message}");
                }
            }
        }
    }

    private static void MigrateLegacyInstall()
    {
        if (!OperatingSystem.IsLinux() || App.RosemaryData.IsInstalled)
            return;

        string legacy = Path.Combine(Paths.Cache, "Rosemary", "rosemary.AppImage");
        if (!File.Exists(legacy))
            return;

        try
        {
            string destination = App.RosemaryData.ExecutablePath;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(legacy, destination, true);
            MarkExecutable(destination);
            App.Logger.Info($"Moved Rosemary from {legacy} to {destination}");
        }
        catch (Exception ex)
        {
            App.Logger.Warn($"Could not move the previous Rosemary install: {ex.Message}");
        }
    }

    private static void MarkExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private static bool IsExecutable(string? path)
    {
        if (!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            UnixFileMode mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
