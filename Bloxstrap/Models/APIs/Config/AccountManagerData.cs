using Bloxstrap.Integrations;
using Newtonsoft.Json;

namespace Bloxstrap.Models.APIs.Config
{
    public class AccountManagerData
    {
        [JsonProperty("accounts")]
        public List<AccountManagerAccount> Accounts { get; set; } = new();

        [JsonProperty("activeAccountId")]
        public long? ActiveAccountId { get; set; }

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [JsonProperty("currentPlaceId")]
        public long CurrentPlaceId { get; set; }

        [JsonProperty("currentServerInstanceId")]
        public string CurrentServerInstanceId { get; set; } = "";
    }
}