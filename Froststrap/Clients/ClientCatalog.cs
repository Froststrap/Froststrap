// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Launchers;
using Froststrap.Clients.Layouts;
using Froststrap.Clients.Model;
using Froststrap.Clients.Registry;
using Froststrap.Clients.Runtimes;
using Froststrap.Clients.Runtime;
using Froststrap.Clients.Sources;

namespace Froststrap.Clients;

/// <summary>
/// The single place that knows which clients exist on which host. Adding a client means
/// adding a layout and one registration here; no consumer changes.
/// </summary>
internal static class ClientCatalog
{
    public static ClientRegistry Build(ClientEnvironment environment)
    {
        IClientStateStore states = new AppClientStateStore();
        ClientRegistry registry = new(environment.Host);

        switch (environment.Host)
        {
        case HostPlatform.Windows:
            Register(registry, environment, states, new WindowsPlayerLayout(), PlayerCapabilities(), "PlayerState", "Roblox");
            Register(registry, environment, states, new WindowsStudioLayout(), StudioCapabilities(), "StudioState", "Roblox Studio");
            break;

        case HostPlatform.MacOS:
            Register(registry, environment, states, new MacBundlePlayerLayout(), MacPlayerCapabilities(), "PlayerState", "Roblox");
            Register(registry, environment, states, new MacBundleStudioLayout(), MacStudioCapabilities(), "StudioState", "Roblox Studio");
            break;

        case HostPlatform.Linux:
            Register(registry, environment, states, new SoberLayout(), SoberCapabilities(), "PlayerState", "Sober");
            Register(registry, environment, states, new WineStudioLayout(), WineStudioCapabilities(), "StudioState", "Roblox Studio (Wine)", layout => new WineLauncher(layout), () => new WineEnvironment());
            break;
        }

        return registry;
    }

    public static IReadOnlyList<string> StaticDirectoryNames { get; } =
        new IClientLayout[] { new WindowsPlayerLayout(), new WindowsStudioLayout(), new MacBundlePlayerLayout(), new MacBundleStudioLayout() }
            .Select(layout => layout.BinaryType!)
            .ToArray();

    public static IClientLayout DeploymentLayout(HostPlatform host, ClientTarget target) => (host, target) switch
    {
        (HostPlatform.MacOS, ClientTarget.Player) => new MacBundlePlayerLayout(),
        (HostPlatform.MacOS, ClientTarget.Studio) => new MacBundleStudioLayout(),
        (HostPlatform.Linux, ClientTarget.Studio) => new WineStudioLayout(),
        (_, ClientTarget.Studio) => new WindowsStudioLayout(),
        _ => new WindowsPlayerLayout()
    };

    private static void Register(
        ClientRegistry registry,
        ClientEnvironment environment,
        IClientStateStore states,
        IClientLayout layout,
        ClientCapabilities capabilities,
        string stateFileName,
        string displayName,
        Func<IClientLayout, IClientLauncher>? launcher = null,
        Func<IRuntimeEnvironment>? runtimeEnvironment = null)
    {
        registry.Register(new ClientDescriptor
        {
            ClientId = layout.ClientId,
            DisplayName = displayName,
            Target = layout.Target,
            HostPlatforms = [environment.Host],
            StateFileName = stateFileName,
            Factory = () => layout is SoberLayout
                ? new SoberClient(layout, capabilities, displayName, environment, states, stateFileName)
                : new RobloxClient(layout, capabilities, displayName, environment, states, stateFileName, launcher?.Invoke(layout), runtimeEnvironment?.Invoke())
        });
    }

    private static ClientCapabilities PlayerCapabilities() => new()
    {
        RequiresDeploymentApi = true,
        ModSupport = ModSupport.BundleFiles,
        RestoreStrategy = RestoreStrategy.PackageReextract,
        SupportsStudioLaunch = true,
        SupportsCustomIntegrations = true
    };

    private static ClientCapabilities StudioCapabilities() => new()
    {
        RequiresDeploymentApi = true,
        ModSupport = ModSupport.BundleFiles,
        RestoreStrategy = RestoreStrategy.PackageReextract,
        SupportsStudioLaunch = true,
        SupportsCustomIntegrations = true
    };

    private static ClientCapabilities MacPlayerCapabilities() => PlayerCapabilities() with
    {
        RestoreStrategy = RestoreStrategy.VersionBackup
    };

    private static ClientCapabilities MacStudioCapabilities() => StudioCapabilities() with
    {
        RestoreStrategy = RestoreStrategy.VersionBackup
    };

    private static ClientCapabilities SoberCapabilities() => new()
    {
        RequiresDeploymentApi = false,
        ModSupport = ModSupport.OverlayOnly,
        RestoreStrategy = RestoreStrategy.None,
        SupportsStudioLaunch = false,
        SupportsCustomIntegrations = false
    };

    private static ClientCapabilities WineStudioCapabilities() => StudioCapabilities();
}
