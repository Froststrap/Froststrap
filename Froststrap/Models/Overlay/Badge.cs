namespace Froststrap.Models.Overlay
{
    internal class Badge
    {
        public long Id { get; set; }

        public string Name { get; set; } = String.Empty;

        public string Description { get; set; } = String.Empty;

        public string? IconUrl { get; set; }

        public bool Awarded { get; set; }

        public bool AwardedKnown { get; set; }

        public DateTime? AwardedDate { get; set; }

        public double WinRatePercentage { get; set; }

        public long PastDayAwardedCount { get; set; }

        public long AwardedCount { get; set; }

        public string RarityText
        {
            get
            {
                double percentage = WinRatePercentage * 100;

                if (percentage >= 10)
                    return $"{Math.Round(percentage)}%";

                if (percentage >= 1)
                    return $"{percentage:0.0}%";

                return $"{percentage:0.00}%";
            }
        }

        public string PastDayAwardedText => PastDayAwardedCount.ToString("N0", Locale.CurrentCulture);

        public string AwardedCountText => AwardedCount.ToString("N0", Locale.CurrentCulture);

        public string StatusText
        {
            get
            {
                if (!Awarded)
                    return Strings.Menu_Overlay_Badges_NotEarned;

                return AwardedDate is null
                    ? Strings.Menu_Overlay_Badges_Earned
                    : String.Format(Strings.Menu_Overlay_Badges_EarnedOn, AwardedDate.Value.ToLocalTime().ToString("d", Locale.CurrentCulture));
            }
        }
    }
}
