// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.UI.ViewModels;
using Froststrap.Utility;

namespace Froststrap.Models
{
    internal class PlaceInfo(long id, long universeId, string name, string? thumbnailUrl, int thumbnailDecodeWidth = 300) : NotifyPropertyChangedViewModel
    {
        public long Id { get; set; } = id;
        public long UniverseId { get; set; } = universeId;
        public string Name { get; set; } = name;

        public int ThumbnailDecodeWidth { get; } = thumbnailDecodeWidth;

        private string? _thumbnailUrl = thumbnailUrl;
        public string? ThumbnailUrl
        {
            get => _thumbnailUrl;
            set
            {
                if (!SetProperty(ref _thumbnailUrl, value))
                    return;

                _ = LoadThumbnailAsync();
            }
        }

        private Bitmap? _thumbnailBitmap;
        public Bitmap? ThumbnailBitmap
        {
            get => _thumbnailBitmap;
            private set => SetProperty(ref _thumbnailBitmap, value);
        }

        private bool _loadingThumbnail;
        private string? _loadedThumbnailUrl;

        private async Task LoadThumbnailAsync()
        {
            string? url = _thumbnailUrl;

            if (_loadingThumbnail || String.IsNullOrEmpty(url) || _loadedThumbnailUrl == url)
                return;

            _loadingThumbnail = true;

            try
            {
                Bitmap? bitmap = await ImageLoader.LoadAsync(url, ThumbnailDecodeWidth);

                if (bitmap is null)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_thumbnailUrl == url)
                    {
                        ThumbnailBitmap = bitmap;
                        _loadedThumbnailUrl = url;
                    }
                });
            }
            finally
            {
                _loadingThumbnail = false;
            }
        }
    }
}