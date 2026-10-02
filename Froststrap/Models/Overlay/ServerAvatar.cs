using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.UI.ViewModels;
using Froststrap.Utility;

namespace Froststrap.Models.Overlay
{
    internal class ServerAvatar : NotifyPropertyChangedViewModel
    {
        public ServerAvatar(string? imageUrl, string? overflowText)
        {
            OverflowText = overflowText;
            ImageUrl = imageUrl;
        }

        public string? OverflowText { get; }

        public bool IsOverflow => OverflowText is not null;

        private string? _imageUrl;
        public string? ImageUrl
        {
            get => _imageUrl;
            set
            {
                if (_imageUrl == value)
                    return;

                _imageUrl = value;

                OnPropertyChanged(nameof(ImageUrl));

                _ = LoadImageAsync();
            }
        }

        private Bitmap? _imageBitmap;
        public Bitmap? ImageBitmap
        {
            get => _imageBitmap;
            private set
            {
                if (_imageBitmap == value)
                    return;

                _imageBitmap = value;

                OnPropertyChanged(nameof(ImageBitmap));
            }
        }

        private bool _loadingImage;
        private string? _loadedImageUrl;

        private async Task LoadImageAsync()
        {
            string? url = _imageUrl;

            if (_loadingImage || String.IsNullOrEmpty(url) || _loadedImageUrl == url)
                return;

            _loadingImage = true;

            try
            {
                Bitmap? bitmap = await ImageLoader.LoadAsync(url, 112);

                if (bitmap is null)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_imageUrl == url)
                    {
                        ImageBitmap = bitmap;
                        _loadedImageUrl = url;
                    }
                });
            }
            finally
            {
                _loadingImage = false;
            }
        }
    }
}