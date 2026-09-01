using Avalonia.Media.Imaging;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Froststrap.UI.Converters;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    internal class ServerEntry : NotifyPropertyChangedViewModel
    {
        private static readonly SemaphoreSlim _thumbnailSemaphore = new(10);

        private string _extraPlayersText = "";
        private bool _hasExtraPlayers;
        private ObservableCollection<Bitmap> _playerAvatarThumbnails = [];
        private bool _thumbnailsLoading;
        private bool _thumbnailsLoaded;

        public int Number { get; set; }
        public string ServerId { get; set; } = null!;
        public string Players { get; set; } = null!;
        public int PlayingCount { get; set; }
        public string Region { get; set; } = null!;
        public int? DataCenterId { get; set; }
        public string Uptime { get; set; } = "Loading...";
        public ICommand? JoinCommand { get; set; }
        public List<string> PlayerTokens { get; set; } = [];

        public ObservableCollection<Bitmap> PlayerAvatarThumbnails
        {
            get => _playerAvatarThumbnails;
            set => SetProperty(ref _playerAvatarThumbnails, value);
        }

        public string ExtraPlayersText
        {
            get => _extraPlayersText;
            set => SetProperty(ref _extraPlayersText, value);
        }

        public bool HasExtraPlayers
        {
            get => _hasExtraPlayers;
            set => SetProperty(ref _hasExtraPlayers, value);
        }

        public async Task LoadThumbnailsAsync()
        {
            if (_thumbnailsLoading || _thumbnailsLoaded || PlayerTokens.Count == 0)
                return;

            _thumbnailsLoading = true;

            try
            {
                var tokenUrlPairs = new List<(string Token, string Url)>();
                var missingTokens = new List<string>();

                foreach (var token in PlayerTokens)
                {
                    if (UrlToBitmapConverter.TryGetCachedUrl(token, out var url) && !string.IsNullOrEmpty(url))
                    {
                        tokenUrlPairs.Add((token, url));
                    }
                    else
                    {
                        missingTokens.Add(token);
                    }
                }

                if (missingTokens.Count > 0)
                {
                    var requests = missingTokens.Select(t => new ThumbnailRequest
                    {
                        Token = t,
                        Type = ThumbnailType.AvatarHeadShot,
                        Size = "60x60",
                        Format = ThumbnailFormat.Png,
                        IsCircular = true
                    }).ToList();

                    var fetchedUrls = await Thumbnails.GetThumbnailUrlsAsync(requests, CancellationToken.None);
                    if (fetchedUrls != null)
                    {
                        for (int i = 0; i < missingTokens.Count && i < fetchedUrls.Length; i++)
                        {
                            var url = fetchedUrls[i];
                            if (!string.IsNullOrEmpty(url))
                            {
                                tokenUrlPairs.Add((missingTokens[i], url));
                                UrlToBitmapConverter.CacheUrlMapping(missingTokens[i], url);
                            }
                        }
                    }
                }

                var tasks = tokenUrlPairs.Select(async pair =>
                {
                    await _thumbnailSemaphore.WaitAsync();
                    try
                    {
                        return await UrlToBitmapConverter.GetBitmapFromCacheOrDownloadAsync(pair.Url);
                    }
                    finally
                    {
                        _thumbnailSemaphore.Release();
                    }
                });

                var bitmaps = await Task.WhenAll(tasks);

                foreach (var bmp in bitmaps)
                {
                    if (bmp != null)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            PlayerAvatarThumbnails.Add(bmp);
                        });
                    }
                }

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    int extraCount = PlayingCount - PlayerAvatarThumbnails.Count;
                    if (extraCount > 0)
                    {
                        ExtraPlayersText = $"+{extraCount}";
                        HasExtraPlayers = true;
                    }
                    else
                    {
                        HasExtraPlayers = false;
                    }
                });

                _thumbnailsLoaded = true;
            }
            finally
            {
                _thumbnailsLoading = false;
            }
        }
    }
}