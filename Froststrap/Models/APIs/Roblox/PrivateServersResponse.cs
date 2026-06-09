namespace Froststrap.Models.APIs.Roblox
{
    public class PrivateServersResponse
    {
        [JsonPropertyName("data")]
        public List<PrivateServerData> Data { get; set; } = [];
    }
}