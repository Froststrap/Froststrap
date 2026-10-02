using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.UI.ViewModels;
using Froststrap.Utility;

namespace Froststrap.Models.Overlay
{
    internal class Badge : NotifyPropertyChangedViewModel
    {
        public long Id { get; set; }

        public string Name { get; set; } = String.Empty;

        public string Description { get; set; } = String.Empty;

        private string? _iconUrl;
        public string? IconUrl
        {
            get => _iconUrl;
            set => SetProperty(ref _iconUrl, value);
        }

        private Bitmap? _iconBitmap;
        public Bitmap? IconBitmap
        {
            get => _iconBitmap;
            private set => SetProperty(ref _iconBitmap, value);
        }

        public bool Awarded { get; set; }

        public bool AwardedKnown { get; set; }

        public DateTime? AwardedDate { get; set; }

        public double WinRatePercentage { get; set; }

        public long PastDayAwardedCount { get; set; }

        public long AwardedCount { get; set; }

        private bool _loadingIcon;
        private string? _loadedIconUrl;

        public async Task LoadIconAsync()
        {
            if (_loadingIcon || String.IsNullOrEmpty(IconUrl) || _loadedIconUrl == IconUrl)
                return;

            _loadingIcon = true;

            try
            {
                string url = IconUrl;
                Bitmap? bitmap = await ImageLoader.LoadAsync(url, 150);

                if (bitmap is null)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (IconUrl == url)
                    {
                        IconBitmap = bitmap;
                        _loadedIconUrl = url;
                    }
                });
            }
            finally
            {
                _loadingIcon = false;
            }
        }

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
                    : String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Badges_EarnedOn, AwardedDate.Value.ToLocalTime().ToString("d", Locale.CurrentCulture));
            }
        }
    }
}