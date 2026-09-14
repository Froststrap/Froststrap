// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class ClientVersion
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = null!;

        [JsonPropertyName("clientVersionUpload")]
        public string VersionGuid { get; set; } = null!;

        [JsonPropertyName("bootstrapperVersion")]
        public string BootstrapperVersion { get; set; } = null!;

        public DateTime? Timestamp { get; set; }

        public bool IsBehindDefaultChannel { get; set; }
    }
}
