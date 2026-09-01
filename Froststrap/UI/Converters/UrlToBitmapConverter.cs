using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Collections.Concurrent;

namespace Froststrap.UI.Converters
{
    internal class UrlToBitmapConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, Bitmap?> _imageCache = new();
        private static readonly ConcurrentDictionary<string, string> _tokenToUrlCache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrEmpty(url))
                return null;

            try
            {
                if (_imageCache.TryGetValue(url, out var cachedBitmap))
                    return cachedBitmap;

                using var response = App.HttpClient.GetAsync(new Uri(url)).Result;

                Bitmap? bitmap = null;
                if (response.IsSuccessStatusCode)
                {
                    using var stream = response.Content.ReadAsStreamAsync().Result;
                    using var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                    bitmap = new Bitmap(memoryStream);
                }

                // Cache the result (even if null)
                _imageCache.TryAdd(url, bitmap);
                return bitmap;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load image from {url}: {ex.Message}");
                _imageCache.TryAdd(url, null);
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public static bool TryGetCachedUrl(string token, out string? url)
            => _tokenToUrlCache.TryGetValue(token, out url);

        public static void CacheUrlMapping(string token, string url)
            => _tokenToUrlCache.TryAdd(token, url);

        public static async Task<Bitmap?> GetBitmapFromCacheOrDownloadAsync(string url)
        {
            if (_imageCache.TryGetValue(url, out var cached))
                return cached;

            try
            {
                var response = await App.HttpClient.GetAsync(new Uri(url));
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
            catch
            {
                _imageCache.TryAdd(url, null);
                return null;
            }
        }
    }
}
