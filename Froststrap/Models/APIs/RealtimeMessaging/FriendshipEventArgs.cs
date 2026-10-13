namespace Froststrap.Models.APIs.RealtimeMessaging
{
    internal class FriendshipEventArgs
    {
        [JsonPropertyName("UserId1")]
        public long UserId1 { get; set; }

        [JsonPropertyName("UserId2")]
        public long UserId2 { get; set; }

        [JsonPropertyName("UserId1IsActor")]
        public bool UserId1IsActor { get; set; }

        [JsonPropertyName("SourceType")]
        public string? SourceType { get; set; }

        [JsonPropertyName("IsTrustedFriend")]
        public bool IsTrustedFriend { get; set; }
    }
}