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
        public string DurationText
        {
            get
            {
                if (TimeLeft == null) return "Currently Playing";
                var diff = TimeLeft.Value - JoinedAt;
                if (diff.TotalMinutes < 1) return "Less than a minute";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m played";
                return $"{(int)diff.TotalHours}h {diff.Minutes}m played";
            }
        }
    }
}