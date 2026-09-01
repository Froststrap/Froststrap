namespace Froststrap.Models.APIs.Roblox
{
    internal class SortGroup
    {
        [JsonPropertyName("sortId")]
        public string SortId { get; set; } = "";

        [JsonPropertyName("games")]
        public List<RecentlyVisitedGame> Games { get; set; } = [];
    }
}