namespace Froststrap.Models.APIs.Roblox
{
    internal class PresenceNotification
    {
        [JsonPropertyName("UserId")]
        public long UserId { get; set; }

        [JsonPropertyName("Type")]
        public string Type { get; set; } = String.Empty;

        [JsonPropertyName("PresenceReport")]
        public UserPresence? PresenceReport { get; set; }

        [JsonPropertyName("SessionStarted")]
        public DateTime? SessionStarted { get; set; }
    }
}