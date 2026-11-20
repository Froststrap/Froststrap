// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;
using Froststrap.Clients.Registry;
using Froststrap.Clients.Runtime;

namespace Froststrap.Clients.Runtimes
{
    /// <summary>
    /// Sober is installed and updated by flatpak, so "is it installed" is a different question
    /// from "is there a version directory". It is answered here rather than in shared code.
    /// </summary>
    internal sealed class SoberClient(
        IClientLayout layout,
        ClientCapabilities capabilities,
        string displayName,
        ClientEnvironment environment,
        IClientStateStore stateStore,
        string stateFileName)
        : RobloxClient(layout, capabilities, displayName, environment, stateStore, stateFileName)
    {
        public override ClientInspection Inspect()
        {
            ClientInstall install = Install;
            string clientPath = Path.Combine(install.InstallRoot, "data", "sober", "packages", "x86_64", "com.roblox.client");

            bool installed;
            try
            {
                installed = Directory.Exists(clientPath)
                    && Directory.EnumerateFiles(clientPath, "*", SearchOption.AllDirectories).Any();
            }
            catch (Exception ex)
            {
                return new ClientInspection
                {
                    Install = install,
                    IsInstalled = false,
                    IsStateSaved = IsStateSaved,
                    Reason = InstallReason.Missing,
                    Error = ex.Message
                };
            }

            return new ClientInspection
            {
                Install = install,
                IsInstalled = installed,
                IsStateSaved = IsStateSaved,
                Reason = installed ? InstallReason.None : InstallReason.Missing
            };
        }
    }
}
