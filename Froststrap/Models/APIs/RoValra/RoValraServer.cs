// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RoValra
{
    internal class RoValrasServer
    {
        [JsonPropertyName("first_seen")]
        public DateTime? FirstSeen { get; set; }

        [JsonPropertyName("server_id")]
        public string? ServerId { get; set; }
    }
}