namespace Froststrap.Models.APIs.Roblox
{
    public class PresenceResponse
    {
        [JsonPropertyName("userPresences")]
        public List<UserPresence> UserPresences { get; set; } = new();
    }
}
