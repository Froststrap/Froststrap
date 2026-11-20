// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;

namespace Froststrap.AppData
{
    internal sealed class RobloxPlayerData(IClientLayout layout) : CommonAppData, IAppData
    {
        public string ProductName => "Roblox";

        public override string BinaryType => layout.BinaryType!;

        public string RegistryName => "RobloxPlayer";

        public string ProcessName => layout.ProcessNames[0];

        public override string ExecutableName => layout.ExecutableName!;

        public override JsonManager<DistributionState> DistributionStateManager => App.PlayerState;
    }
}
