// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models
{
    internal class GameHistoryEntry
    {
        [JsonPropertyName("universeId")]
        public long UniverseId { get; set; }

        [JsonPropertyName("placeId")]
        public long PlaceId { get; set; }

        [JsonPropertyName("servers")]
        public List<ServerInfo> Servers { get; set; } = [];
    }
}