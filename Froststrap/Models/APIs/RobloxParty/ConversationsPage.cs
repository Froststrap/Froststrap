namespace Froststrap.Models.APIs.RobloxParty
{
    internal class ConversationsPage : Page
    {
        [JsonPropertyName("conversations")]
        public List<Conversation> Conversations { get; set; } = new();
    }
}
