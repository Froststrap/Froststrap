// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class FetchResult
    {
        [JsonPropertyName("data")]
        public List<ServerInstance> Servers { get; set; } = [];

        [JsonPropertyName("nextPageCursor")]
        public string NextCursor { get; set; } = string.Empty;

        [JsonIgnore]
        public int NewlyFetchedCount { get; set; }
    }
}