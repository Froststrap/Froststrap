namespace Froststrap.Models
{
    public class ServerInfo
    {
        [JsonPropertyName("jobId")]
        public string JobId { get; set; } = string.Empty;

        [JsonPropertyName("joinedAt")]
        public DateTime JoinedAt { get; set; }

        [JsonPropertyName("timeLeft")]
        public DateTime? TimeLeft { get; set; }

        [JsonPropertyName("ServerType")]
        public ServerType ServerType { get; set; }

        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsLatest { get; set; }

        [JsonIgnore]
        public string DurationText => $"From {JoinedAt:HH:mm} to {TimeLeft:HH:mm}";
    }
}