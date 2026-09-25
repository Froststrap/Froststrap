namespace Froststrap.Models.APIs.RobloxParty
{
    internal class ConversationPayload
    {
        [JsonPropertyName("conversation_id")]
        public string Id { get; set; } = String.Empty;
    }
}
