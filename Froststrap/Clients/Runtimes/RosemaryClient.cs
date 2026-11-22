// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Launchers;
using Froststrap.Clients.Model;
using Froststrap.Clients.Registry;
using Froststrap.Clients.Runtime;

namespace Froststrap.Clients.Runtimes;

internal sealed class RosemaryClient(
    IClientLayout layout,
    ClientCapabilities capabilities,
    string displayName,
    ClientEnvironment environment,
    IClientStateStore stateStore,
    string stateFileName)
    : RobloxClient(layout, capabilities, displayName, environment, stateStore, stateFileName)
{
    private const string ReleaseUrl = "https://api.github.com/repos/microlift/rosemary/releases/latest";
    private const string ChecksumsAssetName = "SHA256SUMS";

    private static readonly TimeSpan ReleaseCheckTimeout = TimeSpan.FromSeconds(10);

    private readonly string _managedPath = Path.Combine(environment.DataRoot, "Rosemary", "rosemary.AppImage");

    private string? _executable;

    private RosemaryLauncher? _rosemaryLauncher;

    public override IClientLauncher Launcher => _rosemaryLauncher ??= new RosemaryLauncher(() => _executable ?? FindExisting());

    public override string LaunchLabel => Strings.Common_Rosemary;

    public override string DescribeLaunchFailure(string details) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Rosemary_LaunchFailed, details);

    public override async Task<InstallResult> EnsureInstalledAsync(IInstallReporter reporter)
    {
        _executable = File.Exists(_managedPath) ? await UpdateManagedAsync(reporter) : FindOnPath() ?? await UpdateManagedAsync(reporter);

        return _executable is null
            ? new InstallResult { Error = Strings.Rosemary_DownloadFailed }
            : new InstallResult { Success = true };
    }

    public override Task FinalizeInstallAsync(string versionDirectory)
    {
        PrepareBundle(versionDirectory);
        return Task.CompletedTask;
    }

    private string? FindExisting() => File.Exists(_managedPath) ? _managedPath : FindOnPath();

    private static string? FindOnPath()
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, "rosemary");

            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private async Task<string?> UpdateManagedAsync(IInstallReporter reporter)
    {
        string? existing = File.Exists(_managedPath) ? _managedPath : null;
        GithubRelease? release;

        try
        {
            using var timeout = new CancellationTokenSource(ReleaseCheckTimeout);
            release = await JsonHelper.GetFromJsonAsync<GithubRelease>(App.HttpClient, ReleaseUrl, timeout.Token);
        }
        catch (Exception ex)
        {
            App.Logger.Warn($"Could not check for Rosemary updates: {ex.Message}");
            return existing;
        }

        if (release is null)
            return existing;

        if (existing is not null && string.Equals(App.State.Prop.RosemaryVersion, release.TagName, StringComparison.Ordinal))
        {
            App.Logger.Info($"Rosemary {release.TagName} is up to date");
            return existing;
        }

        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "aarch64" : "x86_64";
        var appImage = release.Assets?.FirstOrDefault(asset => asset.Name.EndsWith($"-{architecture}.AppImage", StringComparison.Ordinal));
        var checksums = release.Assets?.FirstOrDefault(asset => asset.Name == ChecksumsAssetName);

        if (appImage is null || checksums is null)
        {
            App.Logger.Warn($"Rosemary {release.TagName} has no {architecture} AppImage or {ChecksumsAssetName}");
            return existing;
        }

        string temporary = _managedPath + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_managedPath)!);

            reporter.SetStatus(Strings.Bootstrapper_Status_DownloadingRosemary);
            await reporter.DownloadAsync(appImage.BrowserDownloadUrl, temporary);

            string expected = ExpectedHash(await App.HttpClient.GetStringAsync(new Uri(checksums.BrowserDownloadUrl)), appImage.Name)
                ?? throw new InvalidOperationException($"{ChecksumsAssetName} has no entry for {appImage.Name}");

            string actual;
            await using (var stream = File.OpenRead(temporary))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));

            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new ChecksumFailedException($"{appImage.Name} does not match {ChecksumsAssetName} (expected {expected}, got {actual})");

            File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            File.Move(temporary, _managedPath, true);

            App.State.Prop.RosemaryVersion = release.TagName;
            App.State.Save();

            App.Logger.Info($"Installed Rosemary {release.TagName} to {_managedPath}");
            return _managedPath;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to download Rosemary");
            return existing;
        }
        finally
        {
            reporter.ClearProgress();

            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static string? ExpectedHash(string checksums, string fileName)
    {
        foreach (string line in checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 2 && parts[1].TrimStart('*') == fileName)
                return parts[0];
        }

        return null;
    }
}
