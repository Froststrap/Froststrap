namespace Froststrap.Models.APIs.RobloxParty
{
    internal class MessagesContents
    {
        [JsonPropertyName("conversation_id")]
        public string ConversationId { get; set; } = String.Empty;

        [JsonPropertyName("messages")]
        public MessageContent[] Messages { get; set; } = Array.Empty<MessageContent>();
    }
}
