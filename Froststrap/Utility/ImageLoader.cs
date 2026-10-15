// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;

namespace Froststrap.Utility
{
    internal static class ImageLoader
    {
        private static readonly ConcurrentDictionary<string, Bitmap?> Memory = new();
        private static readonly ConcurrentDictionary<string, Task<Bitmap?>> InFlight = new();

        private const int MaxRetries = 3;
        private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(1);

        public static async Task<Bitmap?> LoadAsync(string? url, int decodeWidth = 0, string? cachePath = null)
        {
            if (String.IsNullOrEmpty(url))
                return null;

            if (Memory.TryGetValue(url, out Bitmap? cached))
                return cached;

            return await InFlight.GetOrAdd(url, _ => LoadCoreAsync(url, decodeWidth, cachePath));
        }

        public static async Task LoadAndSetAsync(string? url, int decodeWidth, Action<Bitmap?> setter)
        {
            if (String.IsNullOrEmpty(url))
            {
                setter(null);
                return;
            }

            Bitmap? bitmap = await LoadAsync(url, decodeWidth);

            if (bitmap is null)
                return;

            await Dispatcher.UIThread.InvokeAsync(() => setter(bitmap));
        }

        private static async Task<Bitmap?> LoadCoreAsync(string url, int decodeWidth, string? cachePath)
        {
            byte[]? bytes = null;

            // Try to read from cache first
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
                // Download with retry logic for transient network errors
                bytes = await DownloadWithRetryAsync(url, cachePath);
                
                if (bytes is null)
                    return null;
            }

            try
            {
                Bitmap? bitmap = await Task.Run(() => Decode(bytes, decodeWidth));

                Memory[url] = bitmap;

                return bitmap;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to decode image from {url}: {ex.Message}");
                return null;
            }
            finally
            {
                InFlight.TryRemove(url, out _);
            }
        }

        private static async Task<byte[]?> DownloadWithRetryAsync(string url, string? cachePath)
        {
            Exception? lastException = null;

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    using var response = await App.HttpClient.GetAsync(new Uri(url));

                    if (!response.IsSuccessStatusCode)
                    {
                        // Don't retry on 4xx client errors
                        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                        {
                            App.Logger.Warn($"Image request failed with {response.StatusCode}: {url}");
                            return null;
                        }
                        
                        // For 5xx server errors, throw to trigger retry
                        throw new HttpRequestException($"Server returned {response.StatusCode}");
                    }

                    byte[] bytes = await response.Content.ReadAsByteArrayAsync();

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

                    return bytes;
                }
                catch (Exception ex) when (IsTransientException(ex))
                {
                    lastException = ex;
                    
                    if (attempt < MaxRetries)
                    {
                        TimeSpan delay = TimeSpan.FromSeconds(BaseRetryDelay.TotalSeconds * Math.Pow(2, attempt));
                        App.Logger.Warn($"Image download failed (attempt {attempt + 1}/{MaxRetries + 1}), retrying in {delay.TotalSeconds}s: {url} - {ex.Message}");
                        await Task.Delay(delay);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    // Non-transient exception, log and return null
                    App.Logger.Error($"Failed to load image from {url}: {ex.Message}");
                    return null;
                }
            }

            App.Logger.Error($"Failed to load image from {url} after {MaxRetries + 1} attempts: {lastException?.Message}");
            return null;
        }

        private static bool IsTransientException(Exception ex)
        {
            return ex switch
            {
                HttpRequestException => true,
                TaskCanceledException => true,
                TimeoutException => true,
                WebException webEx => webEx.Status switch
                {
                    WebExceptionStatus.Timeout => true,
                    WebExceptionStatus.ConnectionClosed => true,
                    WebExceptionStatus.ReceiveFailure => true,
                    WebExceptionStatus.SendFailure => true,
                    WebExceptionStatus.PipelineFailure => true,
                    _ => false
                },
                IOException ioEx => IsTransientIOException(ioEx),
                _ => false
            };
        }

        private static bool IsTransientIOException(IOException ex)
        {
            // Check for common transient socket errors
            return ex.HResult switch
            {
                unchecked((int)0x80004005) => true, // E_FAIL - often network related
                unchecked((int)0x80070057) => true, // E_INVALIDARG - can happen with streams
                unchecked((int)0x80131500) => true, // COR_E_IO - generic IO exception
                _ => false
            };
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
