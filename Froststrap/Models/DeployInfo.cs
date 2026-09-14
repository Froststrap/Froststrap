// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models
{
    internal class DeployInfo
    {
        public string Timestamp { get; set; } = null!;
        public string Version { get; set; } = null!;
        public string VersionGuid { get; set; } = null!;
    }
}
