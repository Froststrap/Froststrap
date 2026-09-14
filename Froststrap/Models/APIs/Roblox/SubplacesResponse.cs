// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class SubplacesResponse
    {
        [JsonPropertyName("previousPageCursor")]
        public string? PreviousPageCursor { get; set; }

        [JsonPropertyName("nextPageCursor")]
        public string? NextPageCursor { get; set; }

        [JsonPropertyName("data")]
        public List<SubplaceData> Data { get; set; } = [];
    }
}
