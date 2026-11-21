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
        Processes = new ClientProcessService(layout);
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

    public IProcessService Processes { get; }

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

    public virtual ClientInspection Inspect()
    {
        ClientInstall install = Install;
        bool installed = IsStateSaved
            && !string.IsNullOrEmpty(install.VersionGuid)
            && Directory.Exists(install.InstallRoot);

        return new ClientInspection
        {
            Install = install,
            IsInstalled = installed,
            IsStateSaved = _stateStore.For(_stateFileName).IsSaved,
            Reason = installed ? InstallReason.None : InstallReason.Missing
        };
    }

    public Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        CancellationToken cancellationToken = default)
        => Launcher.LaunchAsync(Install, request, cancellationToken);

    public virtual Task PrepareEnvironmentAsync() => Task.CompletedTask;

    public virtual bool ResetInstalledPackages() => false;
}
