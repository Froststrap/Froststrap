// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Froststrap.Extensions
{
    static class RobloxIconEx
    {
        public static IReadOnlyCollection<RobloxIcon> Selections =>
        [
            RobloxIcon.IconDefault,
            RobloxIcon.Icon2022,
            RobloxIcon.Icon2019,
            RobloxIcon.Icon2017,
            RobloxIcon.IconLate2015,
            RobloxIcon.IconEarly2015,
            RobloxIcon.Icon2011,
            RobloxIcon.Icon2008,
            RobloxIcon.IconFroststrap,
            RobloxIcon.IconCustom
        ];

        private static readonly Dictionary<RobloxIcon, Bitmap> _cache = [];
        private static readonly Dictionary<RobloxIcon, IntPtr> _hIconCache = [];

        public static Bitmap GetIcon(this RobloxIcon icon)
        {
            if (_cache.TryGetValue(icon, out var cached))
                return cached;

            if (icon == RobloxIcon.IconCustom)
            {
                Bitmap? customIcon = null;
                string location = App.Settings.Prop.RobloxIconCustomLocation;

                if (string.IsNullOrEmpty(location))
                {
                    App.Logger.Warn("Custom icon is not set.");
                }
                else
                {
                    try
                    {
                        customIcon = LoadIconFromFile(location);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error($"Failed to load custom icon! {ex}");
                    }
                }

                var result = customIcon ?? LoadFromResource("IconFroststrap");
                _cache[icon] = result;
                return result;
            }

            var bitmap = icon switch
            {
                RobloxIcon.Icon2008 => LoadFromResource("Icon2008"),
                RobloxIcon.Icon2011 => LoadFromResource("Icon2011"),
                RobloxIcon.IconEarly2015 => LoadFromResource("IconEarly2015"),
                RobloxIcon.IconLate2015 => LoadFromResource("IconLate2015"),
                RobloxIcon.Icon2017 => LoadFromResource("Icon2017"),
                RobloxIcon.Icon2019 => LoadFromResource("Icon2019"),
                RobloxIcon.Icon2022 => LoadFromResource("Icon2022"),
                RobloxIcon.IconFroststrap => LoadFromResource("IconFroststrap"),
                _ => LoadFromResource("Icon2025")
            };

            _cache[icon] = bitmap;
            return bitmap;
        }

        public static unsafe IntPtr GetHIcon(this RobloxIcon icon)
        {
            if (_hIconCache.TryGetValue(icon, out var cached))
                return cached;

            byte[] bytes = LoadIconBytes(icon);
            HICON hIcon = IcoBytesToHIcon(bytes);

            if (hIcon != HICON.Null)
                _hIconCache[icon] = (nint)hIcon.Value;

            return (nint)hIcon.Value;
        }

        private static Bitmap LoadFromResource(string name)
        {
            var uri = new Uri($"avares://Froststrap/Resources/{name}.ico");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }

        private static Bitmap LoadIconFromFile(string path)
        {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }

        private static byte[] LoadIconBytes(RobloxIcon icon)
        {
            if (icon == RobloxIcon.IconCustom)
            {
                string location = App.Settings.Prop.RobloxIconCustomLocation;
                if (!string.IsNullOrEmpty(location) && File.Exists(location))
                {
                    try { return File.ReadAllBytes(location); }
                    catch (Exception ex) { App.Logger.Error($"Failed to read custom icon! {ex}"); }
                }

                return LoadResourceBytes("IconFroststrap");
            }

            string name = icon switch
            {
                RobloxIcon.Icon2008 => "Icon2008",
                RobloxIcon.Icon2011 => "Icon2011",
                RobloxIcon.IconEarly2015 => "IconEarly2015",
                RobloxIcon.IconLate2015 => "IconLate2015",
                RobloxIcon.Icon2017 => "Icon2017",
                RobloxIcon.Icon2019 => "Icon2019",
                RobloxIcon.Icon2022 => "Icon2022",
                RobloxIcon.IconFroststrap => "IconFroststrap",
                _ => "Icon2025"
            };

            return LoadResourceBytes(name);
        }

        private static byte[] LoadResourceBytes(string name)
        {
            var uri = new Uri($"avares://Froststrap/Resources/{name}.ico");
            using var stream = AssetLoader.Open(uri);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static unsafe HICON IcoBytesToHIcon(byte[] icoBytes, int desiredSize = 32)
        {
            if (icoBytes.Length < 6)
                return HICON.Null;

            ushort type = BitConverter.ToUInt16(icoBytes, 2);
            ushort count = BitConverter.ToUInt16(icoBytes, 4);
            if (type != 1 || count == 0)
                return HICON.Null;

            int bestIndex = -1;
            int bestScore = int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                int entryOffset = 6 + i * 16;
                if (entryOffset + 16 > icoBytes.Length)
                    break;

                int w = icoBytes[entryOffset + 0];
                int h = icoBytes[entryOffset + 1];
                if (w == 0) w = 256;
                if (h == 0) h = 256;

                int score = Math.Abs(w - desiredSize) + Math.Abs(h - desiredSize);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                return HICON.Null;

            int bestOffset = 6 + bestIndex * 16;
            uint dataSize = BitConverter.ToUInt32(icoBytes, bestOffset + 8);
            uint dataOffset = BitConverter.ToUInt32(icoBytes, bestOffset + 12);

            if (dataOffset + dataSize > icoBytes.Length || dataSize == 0)
                return HICON.Null;

            byte[] imageData = new byte[dataSize];
            Buffer.BlockCopy(icoBytes, (int)dataOffset, imageData, 0, (int)dataSize);

            fixed (byte* pImage = imageData)
            {
                return PInvoke.CreateIconFromResourceEx(
                    pImage,
                    dataSize,
                    fIcon: true,
                    dwVer: 0x00030000,
                    cxDesired: 0,
                    cyDesired: 0,
                    Flags: 0);
            }
        }

        public static unsafe void InvalidateCustomIcon()
        {
            _cache.Remove(RobloxIcon.IconCustom);

            if (_hIconCache.Remove(RobloxIcon.IconCustom, out var stale) && stale != IntPtr.Zero)
                PInvoke.DestroyIcon(new HICON((void*)stale));
        }
    }
}