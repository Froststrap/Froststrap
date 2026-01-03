namespace Bloxstrap.Models
{
    public class GameHistoryData
    {
        [JsonPropertyName("UniverseId")]
        public long UniverseId { get; set; }

        [JsonPropertyName("PlaceId")]
        public long PlaceId { get; set; }

        [JsonPropertyName("JobId")]
        public string JobId { get; set; } = string.Empty;

        [JsonPropertyName("UserId")]
        public long UserId { get; set; }

        [JsonPropertyName("ServerType")]
        public int ServerType { get; set; }

        [JsonPropertyName("TimeJoined")]
        public DateTime TimeJoined { get; set; }

        [JsonPropertyName("TimeLeft")]
        public DateTime? TimeLeft { get; set; }
    }
}