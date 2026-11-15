// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models
{
    internal sealed class VersionCacheEntry
    {
        public string Version { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}