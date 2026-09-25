namespace Froststrap.Models.APIs.RobloxParty.Events
{
    internal class MessageEvent
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; } = "MessageCreated";

        [JsonPropertyName("Actor")]
        public EventActor? Actor { get; set; }

        [JsonPropertyName("ChannelId")]
        public string ConversationId { get; set; } = String.Empty;

        [JsonPropertyName("IsTyping")]
        public bool? IsTyping { get; set; }
    }
}
