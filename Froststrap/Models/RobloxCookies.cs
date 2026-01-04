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