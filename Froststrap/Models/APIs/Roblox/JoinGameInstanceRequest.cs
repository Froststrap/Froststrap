namespace Froststrap.Models.APIs.Roblox
{
    internal sealed class JoinGameInstanceRequest
    {
        [JsonPropertyName("placeId")]
        public long PlaceId { get; set; }

        [JsonPropertyName("isTeleport")]
        public bool IsTeleport { get; set; }

        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = "";

        [JsonPropertyName("gameJoinAttemptId")]
        public string GameJoinAttemptId { get; set; } = "";
    }
}