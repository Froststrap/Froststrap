namespace Froststrap.Models.APIs.RoValra
{
    internal class RoValraRegionServer
    {
        [JsonPropertyName("server_id")]
        public string? ServerId { get; set; }
        [JsonPropertyName("datacenter_id")]
        public int DatacenterId { get; set; }
        [JsonPropertyName("first_seen")]
        public DateTime FirstSeen { get; set; }
        [JsonPropertyName("ip_address")]
        public string? IpAddress { get; set; }
        [JsonPropertyName("place_version")]
        public int PlaceVersion { get; set; }
        [JsonPropertyName("region")]
        public string? Region { get; set; }
        [JsonPropertyName("region_code")]
        public string? RegionCode { get; set; }
        [JsonPropertyName("city")]
        public string? City { get; set; }
        [JsonPropertyName("country")]
        public string? Country { get; set; }
    }
}