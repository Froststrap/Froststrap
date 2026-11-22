// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RoValra
{
    internal class DatacenterEntry
    {
        [JsonPropertyName("location")]
        public DatacenterLocation Location { get; set; } = new();

        [JsonPropertyName("dataCenterIds")]
        public List<int> DataCenterIds { get; set; } = [];
    }
}
