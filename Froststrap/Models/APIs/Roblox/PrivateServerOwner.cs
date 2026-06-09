namespace Froststrap.Models.APIs.Roblox
{
    public class PrivateServerOwner
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}