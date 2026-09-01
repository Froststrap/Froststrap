namespace Froststrap.Models.APIs.RoValra
{
    internal class RoValrasServer
    {
        [JsonPropertyName("first_seen")]
        public DateTime? FirstSeen { get; set; }

        [JsonPropertyName("server_id")]
        public string? ServerId { get; set; }
    }
}