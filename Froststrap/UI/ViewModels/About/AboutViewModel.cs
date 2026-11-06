// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.ViewModels.About
{
    internal class AboutViewModel : NotifyPropertyChangedViewModel
    {
        public static string Version => $"v{App.Version}";
        public static BuildMetadataAttribute BuildMetadata => App.BuildMetadata;

        public static bool BuildCommitVisibility => App.IsActionBuild;
        public static bool BuildInformationVisibility => !App.IsProductionBuild;
        public static string BuildTimestamp => BuildMetadata.Timestamp.ToFriendlyString();
        public static string BuildCommitHashUrl => $"https://github.com/{App.ProjectRepository}/commit/{BuildMetadata.CommitHash}";
    }
}
