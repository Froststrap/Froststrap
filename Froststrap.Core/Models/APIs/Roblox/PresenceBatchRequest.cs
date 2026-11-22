// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal sealed class PresenceBatchRequest
    {
        [JsonPropertyName("userIds")]
        public long[] UserIds { get; set; } = [];
    }
}