namespace Froststrap.Models.APIs.Roblox
{
    public class UserPresenceResponse
    {
        [JsonPropertyName("userPresences")]
        public List<UserPresence> UserPresences { get; set; } = new();
    }
}
