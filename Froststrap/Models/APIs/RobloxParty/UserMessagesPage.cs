using System;
using System.Collections.Generic;
using System.Text;

namespace Froststrap.Models.APIs.RobloxParty
{
    internal class UserMessagesPage : Page
    {
        [JsonPropertyName("messages")]
        public List<UserMessage> Messages { get; set; } = new();
    }
}
