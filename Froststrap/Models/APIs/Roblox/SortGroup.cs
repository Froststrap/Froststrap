namespace Froststrap.Models.APIs.Roblox
{
    public class SortGroup
    {
        [JsonPropertyName("sortId")]
        public string SortId { get; set; } = "";

        [JsonPropertyName("games")]
        public List<RecentlyVisitedGame> Games { get; set; } = [];
    }
}