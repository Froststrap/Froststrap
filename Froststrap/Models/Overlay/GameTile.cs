using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.UI.ViewModels;
using Froststrap.Utility;

namespace Froststrap.Models.Overlay
{
    internal class GameTile : NotifyPropertyChangedViewModel
    {
        public long UniverseId { get; set; }

        public long PlaceId { get; set; }

        public string Name { get; set; } = String.Empty;

        public long? Playing { get; set; }

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
                Bitmap? bitmap = await ImageLoader.LoadAsync(url, 272);

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

        public string PlayingText => Playing is null
            ? String.Empty
            : String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Games_Playing, Compact(Playing.Value));

        private static string Compact(long value) => value switch
        {
            >= 1_000_000 => (value / 1_000_000d).ToString("0.#", Locale.CurrentCulture) + "M",
            >= 1_000 => (value / 1_000d).ToString("0.#", Locale.CurrentCulture) + "K",
            _ => value.ToString(Locale.CurrentCulture)
        };
    }
}