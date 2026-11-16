// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models
{
    internal class AccountManagerData
    {
        [JsonPropertyName("accounts")]
        public List<AccountManagerAccount> Accounts { get; set; } = [];

        [JsonPropertyName("activeAccountId")]
        public long? ActiveAccountId { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}