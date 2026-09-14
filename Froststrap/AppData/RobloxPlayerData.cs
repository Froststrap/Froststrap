// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.AppData
{
    internal class RobloxPlayerData : CommonAppData, IAppData
    {
        public string ProductName => "Roblox";

        public override string BinaryType => OperatingSystem.IsMacOS() ? "MacPlayer" : "WindowsPlayer";

        public string RegistryName => "RobloxPlayer";

        public string ProcessName => OperatingSystem.IsMacOS() ? "RobloxPlayer" : "RobloxPlayerBeta";

        public override string ExecutableName => App.RobloxPlayerAppName;

        public override JsonManager<DistributionState> DistributionStateManager => App.PlayerState;
    }
}