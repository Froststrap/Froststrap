// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.AppData
{
    internal class RosemaryData : CommonAppData, IAppData
    {
        public string ProductName => "Rosemary";

        public override string BinaryType => "Rosemary";

        public string RegistryName => "Rosemary";

        public string ProcessName => "rosemary";

        public override string ExecutableName => "rosemary.AppImage";

        public static string Root => Path.Combine(Paths.Base, "Rosemary");

        public static string PrefixDirectory => Path.Combine(Root, "Prefix");

        public override string StaticDirectory => Root;

        public override string DynamicDirectory => Root;

        public override string Directory => Root;

        public override bool IsInstalled => File.Exists(ExecutablePath);

        public override JsonManager<DistributionState> DistributionStateManager => App.RosemaryState;
    }
}
