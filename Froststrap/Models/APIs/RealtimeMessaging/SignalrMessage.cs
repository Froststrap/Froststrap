// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RealtimeMessaging
{
    internal sealed class SignalrMessage
    {
        [JsonPropertyName("type")]
        public int? Type { get; set; }

        [JsonPropertyName("target")]
        public string? Target { get; set; }

        [JsonPropertyName("arguments")]
        public string[]? Arguments { get; set; }
    }
}
