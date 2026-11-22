// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RoValra
{
    internal class DatacenterLocation
    {
        [JsonPropertyName("city")]
        public string City { get; set; } = "";

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("latLong")]
        public string[] LatLong { get; set; } = null!;
    }
}
