using System;
using System.Collections.Generic;
using System.Text;

namespace Froststrap.Models.Overlay
{
    internal class GameServer
    {
        public string JobId { get; set; } = String.Empty;

        public int? Playing { get; set; }

        public int? MaxPlayers { get; set; }

        public double? Fps { get; set; }

        public int? Ping { get; set; }

        public string? City { get; set; }

        public string? Region { get; set; }

        public bool IsCurrent { get; set; }

        public List<string> PlayerTokens { get; set; } = new();

        public DateTime? StartedAt { get; set; }

        public bool UptimeIsEstimate { get; set; } = true;

        public bool HasUptime => StartedAt is not null;

        public string UptimeText
        {
            get
            {
                if (StartedAt is null)
                    return String.Empty;

                TimeSpan up = DateTime.UtcNow - StartedAt.Value;

                if (up < TimeSpan.Zero)
                    up = TimeSpan.Zero;

                string span = up.TotalDays >= 1 ? $"{(int)up.TotalDays}d {up.Hours}h"
                    : up.TotalHours >= 1 ? $"{(int)up.TotalHours}h {up.Minutes}m"
                    : $"{Math.Max(up.Minutes, 1)}m";

                return String.Format(UptimeIsEstimate ? Strings.Menu_Overlay_Servers_UptimeEstimate : Strings.Menu_Overlay_Servers_UptimeExact, span);
            }
        }

        public List<string> PlayerIcons { get; } = new();

        public int OverflowCount => HasStats ? Math.Max(Playing!.Value - PlayerIcons.Count, 0) : 0;

        public bool HasOverflow => OverflowCount > 0;

        public string OverflowText => $"+{OverflowCount}";

        public bool HasStats => Playing is not null && MaxPlayers is not null;

        public bool IsFull => HasStats && Playing >= MaxPlayers;

        public string PlayersText => HasStats ? $"{Playing}/{MaxPlayers}" : "—";

        public double FillPercentage => HasStats && MaxPlayers > 0
            ? (double)Playing!.Value / MaxPlayers!.Value * 100
            : 0;

        public string FpsText => Fps is null ? String.Empty : $"{Math.Round(Fps.Value)} FPS";

        public string PingText => Ping is null ? String.Empty : $"{Ping} ms";

        public string LocationText
        {
            get
            {
                if (String.IsNullOrEmpty(City))
                    return String.Empty;

                return String.IsNullOrEmpty(Region) || Region == City ? City : $"{City}, {Region}";
            }
        }

        public string ShortId => JobId.Length > 8 ? JobId[..8] : JobId;
    }
}
