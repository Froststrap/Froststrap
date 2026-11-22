// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RoValra
{
    internal class RoValraRegionResponse
    {
        [JsonPropertyName("servers")]
        public List<RoValraRegionServer>? Servers { get; set; }
        [JsonPropertyName("next_cursor")]
        public int? NextCursor { get; set; }
        [JsonPropertyName("filters")]
        public object? Filters { get; set; }
        [JsonPropertyName("place_id")]
        public string? PlaceId { get; set; }
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}