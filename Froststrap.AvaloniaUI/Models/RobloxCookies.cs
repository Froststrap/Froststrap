using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Froststrap.Models
{
    public class RobloxCookies
    {
        [JsonPropertyName("CookiesVersion")]
        public string Version { get; set; } = null!;

        [JsonPropertyName("CookiesData")]
        public string Cookies { get; set; } = null!;
    }
}