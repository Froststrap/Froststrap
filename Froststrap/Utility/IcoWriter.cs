// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;

namespace Froststrap.Utility
{
    /// <summary>
    /// Writes PNG-compressed frames into an ICO container so the small
    /// resources shipped with the app can be stored as a single PNG.
    /// </summary>
    internal static class IcoWriter
    {
        private static readonly int[] DefaultSizes = [16, 24, 32, 48, 64, 128, 256];

        public static void Write(Bitmap source, Stream output)
        {
            using var sourcePng = new MemoryStream();
            source.Save(sourcePng, PngBitmapEncoderOptions.Default);

            int sourceSize = source.PixelSize.Width;
            List<int> sizes = [];

            foreach (int size in DefaultSizes)
            {
                if (size <= sourceSize)
                    sizes.Add(size);
            }

            if (sizes.Count == 0)
                sizes.Add(sourceSize);

            List<(int Size, byte[] Bytes)> frames = [];

            foreach (int size in sizes)
            {
                sourcePng.Position = 0;
                using var resized = Bitmap.DecodeToWidth(sourcePng, size, BitmapInterpolationMode.HighQuality);
                using var png = new MemoryStream();
                resized.Save(png, PngBitmapEncoderOptions.Default);
                frames.Add((size, png.ToArray()));
            }

            using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);

            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)frames.Count);

            int offset = 6 + frames.Count * 16;

            foreach ((int size, byte[] bytes) in frames)
            {
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)bytes.Length);
                writer.Write((uint)offset);
                offset += bytes.Length;
            }

            foreach ((int _, byte[] bytes) in frames)
                writer.Write(bytes);
        }
    }
}
