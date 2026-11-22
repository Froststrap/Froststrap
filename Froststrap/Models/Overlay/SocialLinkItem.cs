// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using LucideAvalonia.Enum;

namespace Froststrap.Models.Overlay
{
    internal sealed class SocialLinkItem
    {
        public required string Title { get; init; }
        public required string Url { get; init; }
        public required string DisplayUrl { get; init; }
        public required LucideIconNames Icon { get; init; }

        public static SocialLinkItem From(SocialLink link)
        {
            return new SocialLinkItem
            {
                Title = String.IsNullOrWhiteSpace(link.Title) ? link.Type : link.Title,
                Url = link.Url,
                DisplayUrl = Host(link.Url),
                Icon = IconFor(link.Type)
            };
        }

        private static string Host(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && !String.IsNullOrEmpty(uri.Host)
                ? uri.Host
                : url;

        private static LucideIconNames IconFor(string type) => type.ToUpperInvariant() switch
        {
            "DISCORD" => LucideIconNames.MessagesSquare,
            "YOUTUBE" => LucideIconNames.Video,
            "TWITCH" => LucideIconNames.Tv,
            "TWITTER" or "X" => LucideIconNames.AtSign,
            "FACEBOOK" => LucideIconNames.Users,
            "INSTAGRAM" => LucideIconNames.Camera,
            "GUILDED" => LucideIconNames.Shield,
            "ROBLOXGROUP" => LucideIconNames.Users,
            _ => LucideIconNames.Link
        };
    }
}
