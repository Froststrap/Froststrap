using System;
using System.Collections.Generic;
using System.Text;

namespace Froststrap.Models.APIs.RoValra
{
    public class RoValraDatacenter
    {
        [JsonPropertyName("location")]
        public RoValraDatacenterLocation Location { get; set; } = null!;
    }

    public class RoValraDatacenterLocation
    {
        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("latLong")]
        public string[] LatLong { get; set; } = null!;

        [JsonIgnore]
        public double Latitude
        {
            get
            {
                if (LatLong != null && LatLong.Length > 0 && double.TryParse(LatLong[0], out double lat))
                {
                    return lat;
                }
                return 0;
            }
        }

        [JsonIgnore]
        public double Longitude
        {
            get
            {
                if (LatLong != null && LatLong.Length > 1 && double.TryParse(LatLong[1], out double lon))
                {
                    return lon;
                }
                return 0;
            }
        }
    }

    public class RoValraServers
    {
        [JsonPropertyName("servers")]
        public List<RoValraServer> Servers { get; set; } = null!;
    }

    public class RoValraServer
    {
        [JsonPropertyName("server_id")]
        public string ServerId { get; set; } = string.Empty;
    }
}