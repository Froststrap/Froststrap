// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace Froststrap.Models.APIs.RobloxParty
{
    internal class UserMessagesPage : Page
    {
        [JsonPropertyName("messages")]
        public List<UserMessage> Messages { get; set; } = [];
    }
}
