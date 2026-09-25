namespace Froststrap.Models.APIs.RobloxParty
{
    internal class MessageContent
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = String.Empty;
    }
}
