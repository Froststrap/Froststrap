using Froststrap.Integrations;

namespace Froststrap.Models
{
    public class AccountManagerData
    {
        [JsonPropertyName("accounts")]
        public List<AccountManagerAccount> Accounts { get; set; } = new();

        [JsonPropertyName("activeAccountId")]
        public long? ActiveAccountId { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("currentPlaceId")]
        public long? CurrentPlaceId { get; set; }

        [JsonPropertyName("currentServerInstanceId")]
        public string CurrentServerInstanceId { get; set; } = "";
    }
}