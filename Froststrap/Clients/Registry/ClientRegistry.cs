// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Registry;

/// <summary>
/// Resolves one client per target for the current host. Resolution is explicit and cached;
/// there is no global "active client" flag.
/// </summary>
internal sealed class ClientRegistry(HostPlatform host)
{
    private readonly List<ClientDescriptor> _descriptors = [];
    private readonly Dictionary<ClientTarget, IRobloxClient> _resolved = [];

    public HostPlatform Host => host;

    public IReadOnlyList<ClientDescriptor> Available => _descriptors;

    public void Register(ClientDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        bool duplicate = _descriptors.Any(existing =>
            existing.Target == descriptor.Target
            && existing.ClientId == descriptor.ClientId
            && existing.HostPlatforms.Contains(host));

        if (duplicate)
            throw new InvalidOperationException(
                $"Client '{descriptor.ClientId}' for {descriptor.Target} is already registered on {host}.");

        _descriptors.Add(descriptor);
    }

    public IRobloxClient Resolve(ClientTarget target)
    {
        if (_resolved.TryGetValue(target, out IRobloxClient? client))
            return client;

        ClientDescriptor? descriptor = _descriptors.FirstOrDefault(
            candidate => candidate.Target == target && candidate.HostPlatforms.Contains(host));

        if (descriptor is null)
            throw new InvalidOperationException($"No client registered for {target} on {host}.");

        client = descriptor.Factory();
        _resolved[target] = client;
        return client;
    }

    public ClientResolution Describe(ClientTarget target)
    {
        IRobloxClient client = Resolve(target);

        return new ClientResolution
        {
            Host = host,
            Target = target,
            ClientId = client.ClientId,
            DisplayName = client.DisplayName,
            Capabilities = client.Capabilities
        };
    }

    /// <summary>The client that runs Player launches on this host.</summary>
    public IRobloxClient Player => Resolve(ClientTarget.Player);

    /// <summary>The client that runs Studio launches on this host.</summary>
    public IRobloxClient Studio => Resolve(ClientTarget.Studio);

    public IReadOnlyList<DistributionState> AllStates => [Player.State, Studio.State];

    public IAppData PlayerDeployment => _playerDeployment ??= new RobloxPlayerData(ClientCatalog.DeploymentLayout(host, ClientTarget.Player));

    public IAppData StudioDeployment => _studioDeployment ??= new RobloxStudioData(ClientCatalog.DeploymentLayout(host, ClientTarget.Studio));

    private IAppData? _playerDeployment;
    private IAppData? _studioDeployment;
}
