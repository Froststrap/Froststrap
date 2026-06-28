namespace Froststrap.Models.APIs.Roblox
{
    public class AuthenticatedUser
    {
        [JsonPropertyName("id")]
        public long Id { get; set; } = 0;

        [JsonPropertyName("name")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;
    }
}