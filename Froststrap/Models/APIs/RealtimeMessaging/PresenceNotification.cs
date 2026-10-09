namespace Froststrap.Models.APIs.RealtimeMessaging
{
    internal class PresenceNotification
    {
        public const string PresenceChangedType = "PresenceChanged";

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