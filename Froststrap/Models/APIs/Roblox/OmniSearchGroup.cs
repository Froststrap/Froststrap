namespace Froststrap.Models.APIs.Roblox
{
    internal class OmniSearchGroup
    {
        [JsonPropertyName("contents")]
        public List<OmniSearchContent>? Contents { get; set; }
    }
}