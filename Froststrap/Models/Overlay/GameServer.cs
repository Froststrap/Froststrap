using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Froststrap.Resources;

namespace Froststrap.Models.Overlay
{
    internal class GameServer : INotifyPropertyChanged
    {
        private const int AvatarSlots = 6;
        private const int ShortIdLength = 8;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string JobId { get; set; } = String.Empty;
        public int? Playing { get; set; }
        public int? MaxPlayers { get; set; }
        public double? Fps { get; set; }
        public int? Ping { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public int? PlaceVersion { get; set; }
        public bool IsCurrent { get; set; }
        public List<string> PlayerTokens { get; set; } = [];
        public DateTime? StartedAt { get; set; }
        public bool UptimeIsEstimate { get; set; } = true;

        public bool HasUptime => StartedAt is not null;

        public string UptimeText
        {
            get
            {
                if (StartedAt is null) return String.Empty;

                TimeSpan up = DateTime.UtcNow - StartedAt.Value;
                if (up < TimeSpan.Zero) up = TimeSpan.Zero;

                string span = up.TotalDays >= 1 ? $"{(int)up.TotalDays}d {up.Hours}h {up.Minutes}m {up.Seconds}s"
                    : up.TotalHours >= 1 ? $"{up.Hours}h {up.Minutes}m {up.Seconds}s"
                    : up.TotalMinutes >= 1 ? $"{up.Minutes}m {up.Seconds}s"
                    : $"{up.Seconds}s";

                return UptimeIsEstimate
                    ? String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Servers_UptimeEstimate, span)
                    : span;
            }
        }

        public int? Performance => Fps is null ? null : (int)Math.Min(100, Math.Round(Fps.Value / 60 * 100));
        public bool HasPerformance => Performance is not null;
        public bool IsLowPerformance => Performance < 50;
        public string PerformanceText => String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Servers_Performance, Performance);

        public string FpsText => Fps is null
            ? String.Empty
            : String.Format(Locale.CurrentCulture, "{0:0} FPS", Fps.Value);

        public string PingText => Ping is null
            ? String.Empty
            : String.Format(Locale.CurrentCulture, "{0} ms", Ping.Value);

        public bool HasVersion => PlaceVersion is not null;
        public string VersionText => String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Servers_Version, PlaceVersion);

        public List<string> PlayerIcons { get; } = [];

        public int OverflowCount => HasStats ? Math.Max(Playing!.Value - PlayerIcons.Count, 0) : 0;

        public bool HasOverflow => OverflowCount > 0;

        public string OverflowText => OverflowCount > 0
            ? String.Format(Locale.CurrentCulture, "+{0}", OverflowCount)
            : String.Empty;

        public IReadOnlyList<ServerAvatar> Avatars
        {
            get
            {
                int overflow = OverflowCount;
                int faces = overflow > 0 ? AvatarSlots - 1 : AvatarSlots;

                var avatars = PlayerIcons
                    .Take(faces)
                    .Select(url => new ServerAvatar(url, null))
                    .ToList();

                int hidden = HasStats ? Playing!.Value - avatars.Count : 0;
                if (hidden > 0)
                    avatars.Add(new ServerAvatar(null, $"+{hidden}"));

                return avatars;
            }
        }

        public bool HasStats => Playing is not null && MaxPlayers is not null;
        public bool IsFull => HasStats && Playing >= MaxPlayers;

        public string PlayersText => CapacityText;

        public string CapacityText => HasStats
            ? String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Servers_Capacity, Playing, MaxPlayers)
            : Strings.Menu_Overlay_Servers_CapacityUnknown;

        public double FillPercentage => HasStats && MaxPlayers > 0
            ? (double)Playing!.Value / MaxPlayers!.Value * 100
            : 0;

        public string LocationText
        {
            get
            {
                bool hasCity = !String.IsNullOrWhiteSpace(City);
                bool hasRegion = !String.IsNullOrWhiteSpace(Region);

                if (hasCity && hasRegion) return $"{City}, {Region}";
                if (hasCity) return City!;
                if (hasRegion) return Region!;

                return String.Empty;
            }
        }

        public string ShortId => String.IsNullOrEmpty(JobId)
            ? String.Empty
            : JobId.Length > ShortIdLength ? JobId[..ShortIdLength] : JobId;

        public string IdText => String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Servers_Id, JobId);

        public void Tick()
        {
            if (HasUptime)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UptimeText)));
        }
    }
}