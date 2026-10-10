// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Media;

namespace Froststrap.Extensions
{
    internal static class IconHelpers
    {
        public static IImage GetImageSource(this Bitmap bitmap)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            return bitmap;
        }
    }
}
