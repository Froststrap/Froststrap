namespace Froststrap.Models.APIs.Roblox
{
    public class AuthenticatedUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; } = 0;

        [JsonPropertyName("name")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("displayname")]
        public string Displayname { get; set; } = string.Empty;
    }
}