namespace Froststrap.Models.APIs.Roblox
{
    public class UserChannel
    {
        [JsonPropertyName("channelName")]
        public string Channel { get; set; } = "production";

        [JsonPropertyName("channelAssignmentType")]
        public int? AssignmentType { get; set; }

        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }
}