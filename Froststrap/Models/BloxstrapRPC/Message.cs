// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.BloxstrapRPC
{
    internal class Message
    {
        [JsonPropertyName("command")]
        public string Command { get; set; } = null!;

        [JsonPropertyName("data")]
        public JsonElement Data { get; set; }
    }
}