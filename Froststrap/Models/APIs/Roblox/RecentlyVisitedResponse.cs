namespace Froststrap.Models.APIs.Roblox
{
    public class RecentlyVisitedResponse
    {
        [JsonPropertyName("sorts")]
        public List<SortGroup> Sorts { get; set; } = [];
    }
}