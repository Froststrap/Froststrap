// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models
{
    internal class QuickPlayGameItem
    {
        public long UniverseId { get; set; }
        public long PlaceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public long Playing { get; set; }
        public long Visits { get; set; }
        public int ServerCount { get; set; }
        public string? LastJobId { get; set; }
        public UniverseDetails? OriginalDetails { get; set; }
        public GameSource Source { get; set; }
        public long LastPlayedTicks { get; set; }
    }
}