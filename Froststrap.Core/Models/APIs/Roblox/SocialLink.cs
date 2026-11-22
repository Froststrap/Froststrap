// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class SocialLink
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = null!;

        [JsonPropertyName("url")]
        public string Url { get; set; } = null!;

        [JsonPropertyName("title")]
        public string Title { get; set; } = null!;
    }
}
