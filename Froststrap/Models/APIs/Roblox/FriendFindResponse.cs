// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class FriendFindResponse
    {
        [JsonPropertyName("PreviousCursor")]
        public string? PreviousCursor { get; set; }

        [JsonPropertyName("PageItems")]
        public List<FriendFindItem> PageItems { get; set; } = [];

        [JsonPropertyName("NextCursor")]
        public string? NextCursor { get; set; }
    }

    internal class FriendFindItem
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }
}
