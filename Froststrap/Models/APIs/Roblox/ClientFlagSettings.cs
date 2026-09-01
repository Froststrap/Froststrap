namespace Froststrap.Models.APIs.Roblox
{
    internal class ClientFlagSettings
    {
        [JsonPropertyName("applicationSettings")]
        public Dictionary<string, string>? ApplicationSettings { get; set; }
    }
}
