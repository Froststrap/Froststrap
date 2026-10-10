// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Froststrap.UI.Converters;

namespace Froststrap.UI.Utility
{
    internal static class RemoteImage
    {
        public static readonly AttachedProperty<string?> SourceUrlProperty =
            AvaloniaProperty.RegisterAttached<Image, string?>("SourceUrl", typeof(RemoteImage));

        public static string? GetSourceUrl(Image image) => image.GetValue(SourceUrlProperty);
        public static void SetSourceUrl(Image image, string? value) => image.SetValue(SourceUrlProperty, value);

        static RemoteImage()
        {
            SourceUrlProperty.Changed.AddClassHandler<Image>(OnSourceUrlChanged);
        }

        private static void OnSourceUrlChanged(Image image, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.NewValue is not string url || string.IsNullOrEmpty(url))
            {
                image.Source = null;
                return;
            }

            if (UrlToBitmapConverter.TryGetCachedBitmap(url, out var cachedBitmap))
            {
                image.Source = cachedBitmap;
                return;
            }

            _ = LoadAsync(image, url);
        }

        private static async Task LoadAsync(Image image, string url)
        {
            var bitmap = await UrlToBitmapConverter.GetBitmapFromCacheOrDownloadAsync(url);

            if (image.GetValue(SourceUrlProperty) == url)
                image.Source = bitmap;
        }
    }
}
