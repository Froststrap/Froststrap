namespace Froststrap.Models.APIs.Roblox
{
    internal class PrivateServersResponse
    {
        [JsonPropertyName("data")]
        public List<PrivateServerData> Data { get; set; } = [];
    }
}