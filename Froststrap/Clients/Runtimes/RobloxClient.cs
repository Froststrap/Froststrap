// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;
using Froststrap.Clients.Providers;
using Froststrap.Clients.Registry;
using Froststrap.Clients.Runtime;
using Froststrap.Models.Persistable;

namespace Froststrap.Clients.Runtimes
{
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
            string stateFileName)
        {
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

        public ClientTarget Target => Layout.Target;

        public IClientLayout Layout { get; }

        public ClientCapabilities Capabilities { get; }

        public ILogLocator Logs { get; }

        public IProcessService Processes { get; }

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

        public Task<ClientInspection> InspectAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Inspect());

        /// <summary>
        /// Not implemented yet. The CDN install pipeline still lives in the bootstrapper and is
        /// extracted in Step 4 of the plan; wiring it here first would duplicate it.
        /// </summary>
        public Task<InstallResult> EnsureInstalledAsync(
            InstallRequest request,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                $"Installation for '{ClientId}' still runs through the bootstrapper (Step 4).");

        /// <summary>
        /// Not implemented yet. Launch mechanics, readiness, and matchmaking stay in the
        /// bootstrapper until Step 5.
        /// </summary>
        public Task<LaunchResult> LaunchAsync(
            LaunchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                $"Launching '{ClientId}' still runs through the bootstrapper (Step 5).");

        public virtual Task PrepareEnvironmentAsync() => Task.CompletedTask;
    }
}
