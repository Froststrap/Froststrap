namespace Froststrap.Models.APIs.Roblox
{
    internal class RecentlyVisitedResponse
    {
        [JsonPropertyName("sorts")]
        public List<SortGroup> Sorts { get; set; } = [];
    }
}