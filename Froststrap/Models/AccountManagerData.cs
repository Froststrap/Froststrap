using Froststrap.Integrations;

namespace Froststrap.Models
{
    public class AccountManagerData
    {
        [JsonPropertyName("accounts")]
        public List<AccountManagerAccount> Accounts { get; set; } = [];

        [JsonPropertyName("activeAccountId")]
        public long? ActiveAccountId { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}