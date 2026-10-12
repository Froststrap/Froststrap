// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

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

        private string _creator = String.Empty;
        public string Creator
        {
            get => _creator;
            set
            {
                if (SetProperty(ref _creator, value))
                    OnPropertyChanged(nameof(HasCreator));
            }
        }

        private bool _isVerified;
        public bool IsVerified
        {
            get => _isVerified;
            set => SetProperty(ref _isVerified, value);
        }

        private long _favoritedCount;
        public long FavoritedCount
        {
            get => _favoritedCount;
            set
            {
                if (SetProperty(ref _favoritedCount, value))
                {
                    OnPropertyChanged(nameof(FavoritesText));
                    OnPropertyChanged(nameof(HasFavorites));
                }
            }
        }

        private double _likeRatio;
        public double LikeRatio
        {
            get => _likeRatio;
            set
            {
                if (SetProperty(ref _likeRatio, value))
                {
                    OnPropertyChanged(nameof(LikeRatioText));
                    OnPropertyChanged(nameof(HasLikeRatio));
                }
            }
        }

        private bool _isJoiningRegion;
        public bool IsJoiningRegion
        {
            get => _isJoiningRegion;
            set
            {
                if (SetProperty(ref _isJoiningRegion, value))
                    OnPropertyChanged(nameof(IsRegionIdle));
            }
        }

        public bool IsRegionIdle => !_isJoiningRegion;

        public bool HasCreator => !String.IsNullOrEmpty(_creator);

        public bool HasPlaying => Playing is not null;

        public bool HasFavorites => _favoritedCount > 0;

        public string FavoritesText => _favoritedCount.ToString("N0", Locale.CurrentCulture);

        public bool HasLikeRatio => _likeRatio > 0;

        public string LikeRatioText => $"{_likeRatio:0}%";

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
                Bitmap? bitmap = await ImageLoader.LoadAsync(url, 160);

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
    }
}