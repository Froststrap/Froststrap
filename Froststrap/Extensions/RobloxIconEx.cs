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
            HICON hIcon = PngBytesToHIcon(bytes);

            if (hIcon != HICON.Null)
                _hIconCache[icon] = (nint)hIcon.Value;

            return (nint)hIcon.Value;
        }

        private static Bitmap LoadFromResource(string name)
        {
            var uri = new Uri($"avares://Froststrap/Resources/{name}.png");
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
                    try { return ConvertToPngBytes(File.ReadAllBytes(location)); }
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
            var uri = new Uri($"avares://Froststrap/Resources/{name}.png");
            using var stream = AssetLoader.Open(uri);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static byte[] ConvertToPngBytes(byte[] data)
        {
            if (IsPng(data))
                return data;

            byte[]? largestFrame = TryGetLargestIcoFrame(data, out int width, out int height);

            if (largestFrame is not null)
            {
                if (IsPng(largestFrame))
                    return largestFrame;

                if (!TryBuildSingleEntryIco(largestFrame, width, height, out byte[] singleEntryIco))
                    return data;

                data = singleEntryIco;
            }

            using var input = new MemoryStream(data);
            using var bitmap = new Bitmap(input);
            using var output = new MemoryStream();
            bitmap.Save(output, PngBitmapEncoderOptions.Default);
            return output.ToArray();
        }

        private static bool IsPng(byte[] data) =>
            data.Length >= 8 &&
            data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;

        private static byte[]? TryGetLargestIcoFrame(byte[] data, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (data.Length < 6 ||
                BitConverter.ToUInt16(data, 0) != 0 ||
                BitConverter.ToUInt16(data, 2) != 1)
                return null;

            ushort count = BitConverter.ToUInt16(data, 4);
            if (count == 0)
                return null;

            int bestArea = -1;
            int bestWidth = 0;
            int bestHeight = 0;
            int bestOffset = 0;
            int bestSize = 0;

            for (int i = 0; i < count; i++)
            {
                int entryOffset = 6 + i * 16;
                if (entryOffset + 16 > data.Length)
                    break;

                int w = data[entryOffset] == 0 ? 256 : data[entryOffset];
                int h = data[entryOffset + 1] == 0 ? 256 : data[entryOffset + 1];
                uint size = BitConverter.ToUInt32(data, entryOffset + 8);
                uint offset = BitConverter.ToUInt32(data, entryOffset + 12);

                if (size == 0 || offset + size > data.Length)
                    continue;

                if (w * h > bestArea)
                {
                    bestArea = w * h;
                    bestWidth = w;
                    bestHeight = h;
                    bestOffset = (int)offset;
                    bestSize = (int)size;
                }
            }

            if (bestArea < 0)
                return null;

            width = bestWidth;
            height = bestHeight;

            byte[] frame = new byte[bestSize];
            Buffer.BlockCopy(data, bestOffset, frame, 0, bestSize);
            return frame;
        }

        private static bool TryBuildSingleEntryIco(byte[] frame, int width, int height, out byte[] ico)
        {
            ico = [];

            if (frame.Length == 0)
                return false;

            using var output = new MemoryStream();

            using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)1);
                writer.Write((byte)(width >= 256 ? 0 : width));
                writer.Write((byte)(height >= 256 ? 0 : height));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)frame.Length);
                writer.Write((uint)22);
                writer.Write(frame);
            }

            ico = output.ToArray();
            return true;
        }

        private static unsafe HICON PngBytesToHIcon(byte[] pngBytes)
        {
            if (pngBytes.Length == 0)
                return HICON.Null;

            fixed (byte* pImage = pngBytes)
            {
                return PInvoke.CreateIconFromResourceEx(
                    pImage,
                    (uint)pngBytes.Length,
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
