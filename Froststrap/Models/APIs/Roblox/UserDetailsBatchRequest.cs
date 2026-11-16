// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class UserDetailsBatchRequest
    {
        [JsonPropertyName("userIds")]
        public List<long> UserIds { get; set; } = [];

        [JsonPropertyName("excludeBannedUsers")]
        public bool ExcludeBannedUsers { get; set; }
    }
}
