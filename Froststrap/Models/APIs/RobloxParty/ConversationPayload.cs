// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RobloxParty
{
    internal class ConversationPayload
    {
        [JsonPropertyName("conversation_id")]
        public string Id { get; set; } = String.Empty;
    }
}
