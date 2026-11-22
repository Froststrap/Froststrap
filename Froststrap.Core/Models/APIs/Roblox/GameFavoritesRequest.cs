// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal sealed class GameFavoritesRequest
    {
        [JsonPropertyName("isFavorited")]
        public bool IsFavorited { get; set; }
    }
}
