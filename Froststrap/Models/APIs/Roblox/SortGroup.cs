// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class SortGroup
    {
        [JsonPropertyName("sortId")]
        public string SortId { get; set; } = "";

        [JsonPropertyName("games")]
        public List<RecentlyVisitedGame> Games { get; set; } = [];
    }
}