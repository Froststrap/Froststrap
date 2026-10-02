namespace Froststrap.Models.APIs.RobloxParty
{
    internal class UserMessage
    {
        public const long SystemSenderId = 1;

        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = String.Empty;

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("sender_user_id")]
        public long? Sender { get; set; } = SystemSenderId;

        [JsonPropertyName("visibility")]
        public string Visibility { get; set; } = "visible";

        [JsonPropertyName("status")]
        public string Status { get; set; } = String.Empty;
    }
}