// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Launchers;
using Froststrap.Clients.Providers.Channels;
using Froststrap.Clients.Providers.Cookies;
using Froststrap.Clients.Model;
using Froststrap.Clients.Providers;
using Froststrap.Clients.Registry;
using Froststrap.Clients.Runtime;
using Froststrap.Models.Persistable;

namespace Froststrap.Clients.Runtimes;

/// <summary>
/// Data-driven runtime for CDN-managed clients. All packaging, path, log, and process
/// differences come from the layout; this class contains no host checks and no client
/// identity branches.
/// </summary>
internal class RobloxClient : IRobloxClient
{
    private readonly ClientEnvironment _environment;
    private readonly IClientStateStore _stateStore;
    private readonly string _stateFileName;

    public RobloxClient(
        IClientLayout layout,
        ClientCapabilities capabilities,
        string displayName,
        ClientEnvironment environment,
        IClientStateStore stateStore,
        string stateFileName,
        IClientLauncher? launcher = null,
        IRuntimeEnvironment? runtimeEnvironment = null)
    {
        _launcher = launcher;
        RuntimeEnvironment = runtimeEnvironment;
        Layout = layout;
        Capabilities = capabilities;
        DisplayName = displayName;
        _environment = environment;
        _stateStore = stateStore;
        _stateFileName = stateFileName;

        Logs = ClientLogLocators.Create(layout, () => Install.LogsRoot);
    }

    public string ClientId => Layout.ClientId;

    public string DisplayName { get; }

    public virtual string LaunchLabel => Layout.Target == ClientTarget.Studio ? Strings.Common_Studio : Strings.Common_Player;

    public virtual string StartingStatus => Strings.Bootstrapper_Status_Starting;

    public virtual string DescribeLaunchFailure(string details) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_LaunchFailed, DisplayName, details);

    public ClientTarget Target => Layout.Target;

    public IClientLayout Layout { get; }

    public ClientCapabilities Capabilities { get; }

    public ILogLocator Logs { get; }

    public virtual IProcessService Processes => _processes ??= new ClientProcessService(Layout);

    private IProcessService? _processes;

    public virtual IClientLauncher Launcher => _launcher ??= new NativeLauncher(Layout);

    private IClientLauncher? _launcher;

    public ICookieStore Cookies => _cookies ??= CookieStores.Create(Layout.CookieFormat, () => Install);

    private ICookieStore? _cookies;

    public virtual ISettingsSync? SettingsSync => null;

    public IRuntimeEnvironment? RuntimeEnvironment { get; }

    public IHostRegistration Registration => _registration ??= HostRegistrations.Create(Layout);

    private IHostRegistration? _registration;

    public IChannelWriter Channel => _channel ??= ChannelWriters.Create(Layout.ChannelStore, Layout.Target);

    private IChannelWriter? _channel;

    public DistributionState State => _stateStore.For(_stateFileName).Prop;

    public ClientInstall Install => Layout.Resolve(_environment.CreateContext(State));

    protected bool IsStateSaved => _stateStore.For(_stateFileName).IsSaved;

    public virtual bool IsInstalled()
    {
        ClientInstall install = Install;
        return IsStateSaved
            && !string.IsNullOrEmpty(install.VersionGuid)
            && Directory.Exists(install.InstallRoot);
    }

    public Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        CancellationToken cancellationToken = default)
        => Launcher.LaunchAsync(Install, request, cancellationToken);

    public void Kill(int pid) => Processes.Kill(pid);

    public Task CloseAsync(int pid, TimeSpan gracePeriod, CancellationToken cancellationToken = default)
        => Processes.CloseAsync(pid, gracePeriod, cancellationToken);

    public Task CloseAllAsync(KillRequest request, CancellationToken cancellationToken = default)
        => Processes.CloseAllAsync(request, cancellationToken);

    public Task CloseCrashHandlersAsync(CancellationToken cancellationToken = default)
        => Processes.CloseCrashHandlersAsync(cancellationToken);

    public virtual Task<InstallResult> EnsureInstalledAsync(IInstallReporter reporter)
        => Task.FromResult(new InstallResult { Success = true });

    public virtual Task UpdateAsync(IInstallReporter reporter, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual async Task FinalizeInstallAsync(string versionDirectory)
    {
        if (Layout.Shape != ExecutableShape.MacBundle)
            return;

        string appPath = Path.Combine(versionDirectory, Layout.ExecutableName!);
        if (!Directory.Exists(appPath))
            return;

        string macOSDir = Path.Combine(appPath, "Contents", "MacOS");
        if (Directory.Exists(macOSDir)) foreach (string file in Directory.GetFiles(macOSDir))
        {
            var fileInfo = new FileInfo(file);
            fileInfo.UnixFileMode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        }

        if (Layout.SigningTeamId is not null)
            await VerifyBundleSignatureAsync(appPath, Layout.SigningTeamId);

        using var process = Process.Start("xattr", $"-dr com.apple.quarantine \"{appPath}\"");
        await process!.WaitForExitAsync();
    }

    private static async Task VerifyBundleSignatureAsync(string appPath, string teamId)
    {
        var startInfo = new ProcessStartInfo("/usr/bin/codesign")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (string argument in new[] { "--verify", "--deep", "--strict", $"-R=anchor apple generic and certificate leaf[subject.OU] = \"{teamId}\"", appPath })
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        string output = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            App.Logger.Info($"Verified code signature of {appPath} (team {teamId})");
            return;
        }

        App.Logger.Error($"Code signature verification failed for {appPath}: {output.Trim()}");

        try
        {
            Directory.Delete(appPath, true);
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Failed to delete unverified bundle {appPath}");
        }

        throw new ChecksumFailedException($"Failed to verify the code signature of {Path.GetFileName(appPath)}\n\n{output.Trim()}");
    }

    public virtual Task PrepareEnvironmentAsync() => Task.CompletedTask;

    public virtual bool ResetInstalledPackages() => false;
}
