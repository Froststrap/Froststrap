// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs
{
    internal class IPInfoResponse
    {
        [JsonPropertyName("city")]
        public string City { get; set; } = null!;

        [JsonPropertyName("country")]
        public string Country { get; set; } = null!;

        [JsonPropertyName("region")]
        public string Region { get; set; } = null!;

        [JsonPropertyName("loc")]
        public string Loc { get; set; } = null!;
    }
}
