namespace Froststrap.Models.APIs.Roblox
{
    internal class PrivateServerOwner
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}