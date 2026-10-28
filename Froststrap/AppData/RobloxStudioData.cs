// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.AppData
{
    internal class RobloxStudioData : CommonAppData, IAppData
    {
        public string ProductName => "Roblox Studio";

        public override string BinaryType => OperatingSystem.IsMacOS() || App.UseRosemary ? "MacStudio" : "WindowsStudio64";

        public string RegistryName => "RobloxStudio";

        public string ProcessName => OperatingSystem.IsMacOS() || App.UseRosemary ? "RobloxStudio" : "RobloxStudioBeta";

        public override string ExecutableName => App.RobloxStudioAppName;

        public override JsonManager<DistributionState> DistributionStateManager => App.StudioState;
    }
}