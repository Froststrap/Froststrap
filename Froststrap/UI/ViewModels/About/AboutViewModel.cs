// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.ViewModels.About
{
    internal sealed record ContributorItem(string Name, string Url, string? AvatarUrl = null);

    internal sealed class AboutViewModel : NotifyPropertyChangedViewModel
    {
        public static string Version => $"v{App.Version}";
        public static BuildMetadataAttribute BuildMetadata => App.BuildMetadata;

        public static string BuildTimestamp => BuildMetadata.LocalTimestamp.ToFriendlyString();

        public static IReadOnlyList<ContributorItem> CodeContributors { get; } =
        [
            new("meddsam", "https://discord.com/users/1051148571371647017", "https://github.com/RealMeddsam.png"),
            new("SquireDot", "https://github.com/UltimatePlayer97", "https://github.com/UltimatePlayer97.png"),
            new("Invra", "https://invra.net", "https://github.com/invra.png"),
        ];
    }
}
