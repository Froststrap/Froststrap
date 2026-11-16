// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RobloxParty
{
    internal class MessageContent
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = String.Empty;
    }
}
