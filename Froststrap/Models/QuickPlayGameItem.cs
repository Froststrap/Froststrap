// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;

namespace Froststrap.Models
{
    internal sealed class QuickPlayGameItem
    {
        public long UniverseId { get; set; }
        public long PlaceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public Bitmap? ThumbnailBitmap { get; set; }
        public long Playing { get; set; }
        public long Visits { get; set; }
        public int ServerCount { get; set; }
        public string? LastJobId { get; set; }
        public UniverseDetails? OriginalDetails { get; set; }
        public GameSource Source { get; set; }
        public long LastPlayedTicks { get; set; }

        // Playtime tracking
        public long TotalPlaytimeSeconds { get; set; }
        public int SessionCount { get; set; }
        public DateTime? LastPlayedAt { get; set; }

        public string PlaytimeDisplay => FormatPlaytime(TotalPlaytimeSeconds);

        private static string FormatPlaytime(long seconds)
        {
            if (seconds <= 0) return "—";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }
    }
}