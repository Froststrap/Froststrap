namespace Froststrap.Models.APIs.Roblox
{
    internal class UserDetailsBatchRequest
    {
        [JsonPropertyName("userIds")]
        public List<long> UserIds { get; set; } = [];

        [JsonPropertyName("excludeBannedUsers")]
        public bool ExcludeBannedUsers { get; set; }
    }
}
