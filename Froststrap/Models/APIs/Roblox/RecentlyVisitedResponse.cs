// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class RecentlyVisitedResponse
    {
        [JsonPropertyName("sorts")]
        public List<SortGroup> Sorts { get; set; } = [];
    }
}