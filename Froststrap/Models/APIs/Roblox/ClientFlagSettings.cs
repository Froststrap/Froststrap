// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class ClientFlagSettings
    {
        [JsonPropertyName("applicationSettings")]
        public Dictionary<string, string>? ApplicationSettings { get; set; }
    }
}
