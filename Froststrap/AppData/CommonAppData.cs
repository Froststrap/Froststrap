// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.AppData
{
    internal abstract class CommonAppData
    {
        public virtual string ExecutableName { get; } = null!;

        public virtual string BinaryType { get; } = null!;

        public virtual string AppDataDirectory { get; } = Path.Combine(Paths.LocalAppData, "Roblox");

        public string StaticDirectory => Path.Combine(Paths.Versions, BinaryType);
        public string DynamicDirectory => Path.Combine(Paths.Versions, DistributionState.VersionGuid);

        public string Directory => App.Settings.Prop.StaticDirectory ? StaticDirectory : DynamicDirectory;

        public bool IsInstalled => DistributionStateManager.IsSaved && IsInstalledWith(DistributionState);

        public bool IsInstalledWith(DistributionState state)
        {
            if (string.IsNullOrEmpty(state.VersionGuid))
                return false;

            string directory = App.Settings.Prop.StaticDirectory
                ? StaticDirectory
                : Path.Combine(Paths.Versions, state.VersionGuid);

            return System.IO.Directory.Exists(directory);
        }

        public string ExecutablePath => Path.Combine(Directory, ExecutableName);

        public virtual string CdnExtension { get; } = string.Empty;

        public virtual bool SupportsCustomDeployments { get; } = true;

        public virtual JsonManager<DistributionState> DistributionStateManager { get; } = null!;

        public DistributionState DistributionState => DistributionStateManager.Prop;

        public IReadOnlyList<string> ModManifest => DistributionState.ModManifest;
    }
}