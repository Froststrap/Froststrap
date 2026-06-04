namespace Froststrap.Models.APIs.Roblox
{
    public class PrivateServerData
    {
        [JsonPropertyName("vipServerId")]
        public long VipServerId { get; set; }

        [JsonPropertyName("accessCode")]
        public string AccessCode { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("owner")]
        public PrivateServerOwner Owner { get; set; } = new();

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("players")]
        public List<object> Players { get; set; } = [];
    }
}