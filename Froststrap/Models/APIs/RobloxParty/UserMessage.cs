namespace Froststrap.Models.APIs.RobloxParty
{
    internal class UserMessage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = String.Empty;

        [JsonPropertyName("sender_user_id")]
        public long? Sender { get; set; } = SystemSenderId;

        [JsonPropertyName("visibility")]
        public string Visibility { get; set; } = "visible";

        [JsonPropertyName("status")]
        public string Status { get; set; } = String.Empty;

        public const long SystemSenderId = 1;
    }
}
