// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RobloxParty
{
    internal class ConversationsPage : Page
    {
        [JsonPropertyName("conversations")]
        public List<Conversation> Conversations { get; set; } = [];
    }
}
