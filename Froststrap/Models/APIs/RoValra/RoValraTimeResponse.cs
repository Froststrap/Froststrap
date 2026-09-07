// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RoValra
{
    internal class RoValraTimeResponse
    {
        [JsonPropertyName("servers")]
        public List<RoValrasServer>? Servers { get; set; } = null!;

        [JsonPropertyName("status")]
        public string Status = null!;
    }
}