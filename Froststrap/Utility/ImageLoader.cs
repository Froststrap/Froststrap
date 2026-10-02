// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using System.Collections.Concurrent;

namespace Froststrap.Utility
{
    internal static class ImageLoader
    {
        private static readonly ConcurrentDictionary<string, Bitmap?> Memory = new();
        private static readonly ConcurrentDictionary<string, Task<Bitmap?>> InFlight = new();

        public static async Task<Bitmap?> LoadAsync(string? url, int decodeWidth = 0, string? cachePath = null)
        {
            if (String.IsNullOrEmpty(url))
                return null;

            if (Memory.TryGetValue(url, out Bitmap? cached))
                return cached;

            return await InFlight.GetOrAdd(url, _ => LoadCoreAsync(url, decodeWidth, cachePath));
        }

        private static async Task<Bitmap?> LoadCoreAsync(string url, int decodeWidth, string? cachePath)
        {
            try
            {
                byte[]? bytes = null;

                if (!String.IsNullOrEmpty(cachePath) && File.Exists(cachePath))
                {
                    try
                    {
                        bytes = await File.ReadAllBytesAsync(cachePath);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Failed to read cached image {cachePath}: {ex.Message}");
                        try { File.Delete(cachePath); } catch { }
                    }
                }

                if (bytes is null)
                {
                    using var response = await App.HttpClient.GetAsync(new Uri(url));

                    if (!response.IsSuccessStatusCode)
                    {
                        Memory[url] = null;
                        return null;
                    }

                    bytes = await response.Content.ReadAsByteArrayAsync();

                    if (!String.IsNullOrEmpty(cachePath))
                    {
                        try
                        {
                            string? dir = Path.GetDirectoryName(cachePath);

                            if (!String.IsNullOrEmpty(dir))
                                Directory.CreateDirectory(dir);

                            await File.WriteAllBytesAsync(cachePath, bytes);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Error($"Failed to cache image {cachePath}: {ex.Message}");
                        }
                    }
                }

                Bitmap? bitmap = await Task.Run(() => Decode(bytes, decodeWidth));

                Memory[url] = bitmap;

                return bitmap;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load image from {url}: {ex.Message}");
                Memory[url] = null;
                return null;
            }
            finally
            {
                InFlight.TryRemove(url, out _);
            }
        }

        private static Bitmap Decode(byte[] bytes, int decodeWidth)
        {
            using var stream = new MemoryStream(bytes);

            return decodeWidth > 0
                ? Bitmap.DecodeToWidth(stream, decodeWidth)
                : new Bitmap(stream);
        }
    }
}