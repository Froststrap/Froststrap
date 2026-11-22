// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class GameThumbnailResponse
    {
        [JsonPropertyName("universeId")]
        public long UniverseId { get; set; }

        [JsonPropertyName("thumbnails")]
        public List<ThumbnailResponse>? Thumbnails { get; set; }
    }
}
