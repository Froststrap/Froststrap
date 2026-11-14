// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.ViewModels.About
{
    internal class AboutViewModel : NotifyPropertyChangedViewModel
    {
        public static string Version => $"v{App.Version}";
        public static BuildMetadataAttribute BuildMetadata => App.BuildMetadata;

        public static string BuildTimestamp => BuildMetadata.LocalTimestamp.ToFriendlyString();
        public static string BuildCommitHashUrl => $"https://github.com/{ProjectInfo.Repository}/commit/{BuildMetadata.CommitHash}";
    }
}
