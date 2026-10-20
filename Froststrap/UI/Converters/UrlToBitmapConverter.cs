// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Collections.Concurrent;

namespace Froststrap.UI.Converters
{
    internal class UrlToBitmapConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, Bitmap?> _imageCache = new();
        private static readonly ConcurrentDictionary<string, Task<Bitmap?>> _inFlight = new();
        private static readonly ConcurrentDictionary<string, string> _tokenToUrlCache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrEmpty(url))
                return null;

            return TryGetCachedBitmap(url, out var cachedBitmap) ? cachedBitmap : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public static bool TryGetCachedBitmap(string url, out Bitmap? bitmap)
            => _imageCache.TryGetValue(url, out bitmap!);

        public static bool TryGetCachedUrl(string token, out string? url)
            => _tokenToUrlCache.TryGetValue(token, out url);

        public static void CacheUrlMapping(string token, string url)
            => _tokenToUrlCache.TryAdd(token, url);

        public static async Task<Bitmap?> GetBitmapFromCacheOrDownloadAsync(string url)
        {
            if (_imageCache.TryGetValue(url, out var cached))
                return cached;

            var task = _inFlight.GetOrAdd(url, DownloadAsync);
            try
            {
                return await task;
            }
            finally
            {
                _inFlight.TryRemove(url, out _);
            }
        }

        private static async Task<Bitmap?> DownloadAsync(string url)
        {
            try
            {
                using var response = await App.HttpClient.GetAsync(new Uri(url));
                if (!response.IsSuccessStatusCode)
                {
                    _imageCache.TryAdd(url, null);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                var bitmap = new Bitmap(memoryStream);
                _imageCache.TryAdd(url, bitmap);
                return bitmap;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load image from {url}: {ex.Message}");
                _imageCache.TryAdd(url, null);
                return null;
            }
        }
    }
}
