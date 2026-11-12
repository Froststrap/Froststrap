// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs
{
    internal class ThumbnailCacheEntry
    {
        public ulong Id { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}
