using Bloxstrap.Integrations;
using Newtonsoft.Json;

namespace Bloxstrap.Models.APIs.Config
{
    public class AccountManagerData
    {
        [JsonProperty("accounts")]
        public List<AltAccount> Accounts { get; set; } = new();

        [JsonProperty("activeAccountId")]
        public long? ActiveAccountId { get; set; }

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [JsonProperty("currentPlaceId")]
        public string CurrentPlaceId { get; set; } = "";

        [JsonProperty("currentServerInstanceId")]
        public string CurrentServerInstanceId { get; set; } = "";
    }
}