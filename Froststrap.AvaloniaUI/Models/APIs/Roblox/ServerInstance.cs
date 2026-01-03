namespace Froststrap.Models.APIs.Roblox
{
    public class ServerInstance
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("playing")]
        public int Playing { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("region")]
        public string Region { get; set; } = "Unknown";

        [JsonPropertyName("dataCenterId")]
        public int? DataCenterId { get; set; }

        [JsonPropertyName("firstSeen")]
        public DateTime? FirstSeen { get; set; }

        [JsonIgnore]
        public string UptimeDisplay
        {
            get
            {
                if (FirstSeen == null)
                    return "Not Tracked";

                TimeSpan uptime = DateTime.UtcNow - FirstSeen.Value;
                if (uptime.TotalSeconds < 60)
                    return "Just started";

                return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
            }
        }
    }
}
