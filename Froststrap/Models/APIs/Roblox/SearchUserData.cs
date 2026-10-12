namespace Froststrap.Models.APIs.Roblox
{
    internal class SearchUserData
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = String.Empty;

        [JsonPropertyName("hasVerifiedBadge")]
        public bool HasVerifiedBadge { get; set; }
    }
}