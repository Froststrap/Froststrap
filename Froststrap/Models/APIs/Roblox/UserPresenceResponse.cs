namespace Froststrap.Models.APIs.Roblox
{
    internal class UserPresenceResponse
    {
        [JsonPropertyName("userPresences")]
        public List<UserPresence> UserPresences { get; set; } = new();
    }
}
