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
        private const int MaxCacheEntries = 256;

        private sealed class CacheEntry
        {
            public WeakReference<Bitmap>? Bitmap { get; set; }
            public LinkedListNode<string>? Node { get; set; }
        }

        private static readonly Lock _cacheLock = new();
        private static readonly Dictionary<string, CacheEntry> _imageCache = new(StringComparer.Ordinal);
        private static readonly LinkedList<string> _cacheOrder = new();
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
        {
            lock (_cacheLock)
            {
                if (!_imageCache.TryGetValue(url, out CacheEntry? entry))
                {
                    bitmap = null;
                    return false;
                }

                if (entry.Bitmap is { } weak && weak.TryGetTarget(out Bitmap? target))
                {
                    Touch(url, entry);
                    bitmap = target;
                    return true;
                }

                RemoveEntry(url);
                bitmap = null;
                return false;
            }
        }

        public static bool TryGetCachedUrl(string token, out string? url)
            => _tokenToUrlCache.TryGetValue(token, out url);

        public static void CacheUrlMapping(string token, string url)
            => _tokenToUrlCache.TryAdd(token, url);

        public static async Task<Bitmap?> GetBitmapFromCacheOrDownloadAsync(string url)
        {
            if (TryGetCachedBitmap(url, out Bitmap? cached))
                return cached;

            Task<Bitmap?> task = _inFlight.GetOrAdd(url, _ => DownloadAsync(url));
            try
            {
                return await task;
            }
            finally
            {
                _inFlight.TryRemove(new KeyValuePair<string, Task<Bitmap?>>(url, task));
            }
        }

        private static async Task<Bitmap?> DownloadAsync(string url)
        {
            try
            {
                using var response = await App.HttpClient.GetAsync(
                    new Uri(url),
                    HttpCompletionOption.ResponseHeadersRead
                );

                if (!response.IsSuccessStatusCode)
                    return null;

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var bitmap = new Bitmap(memoryStream);
                AddToCache(url, bitmap);
                return bitmap;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load image from {url}: {ex.Message}");
                return null;
            }
        }

        private static void AddToCache(string url, Bitmap bitmap)
        {
            lock (_cacheLock)
            {
                if (_imageCache.TryGetValue(url, out CacheEntry? entry))
                {
                    entry.Bitmap = new WeakReference<Bitmap>(bitmap);
                    Touch(url, entry);
                    return;
                }

                entry = new CacheEntry
                {
                    Bitmap = new WeakReference<Bitmap>(bitmap),
                    Node = _cacheOrder.AddLast(url)
                };

                _imageCache[url] = entry;

                while (_imageCache.Count > MaxCacheEntries && _cacheOrder.First is { } first)
                    RemoveEntry(first.Value);
            }
        }

        private static void Touch(string url, CacheEntry entry)
        {
            if (entry.Node is null)
                return;

            _cacheOrder.Remove(entry.Node);
            _cacheOrder.AddLast(entry.Node);
        }

        private static void RemoveEntry(string url)
        {
            if (_imageCache.Remove(url, out CacheEntry? entry) && entry.Node is not null)
                _cacheOrder.Remove(entry.Node);
        }
    }
}
