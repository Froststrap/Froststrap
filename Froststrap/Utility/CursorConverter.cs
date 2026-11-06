// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using SkiaSharp;
using System.Buffers.Binary;

namespace Froststrap.Utility
{
    /// Converts cursor image files (.png, .ico, .cur, .ani) into PNG bytes,
    /// the only format Roblox's texture pipeline understands.
    internal static class CursorConverter
    {
        public static byte[]? TryConvertToPng(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);

                return Path.GetExtension(path).ToUpperInvariant() switch
                {
                    ".PNG" => ValidatePng(data),
                    ".ICO" or ".CUR" => IconToPng(data),
                    ".ANI" => AnimatedCursorToPng(data),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to convert cursor '{path}': {ex.Message}");
                return null;
            }
        }

        private static byte[]? ValidatePng(byte[] data)
        {
            using var bitmap = SKBitmap.Decode(data);
            return bitmap is null ? null : data;
        }

        private static byte[]? IconToPng(byte[] data)
        {
            // Skia's ICO codec may skip CUR files (2); the container layout is
            // otherwise identical, rewrite the header as ICO (1) before decoding.
            if (data.Length >= 6 && BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4, 2)) == 2)
            {
                data[4] = 1;
                data[5] = 0;
            }

            using var bitmap = SKBitmap.Decode(data);
            return EncodePng(bitmap);
        }

        private static byte[]? AnimatedCursorToPng(byte[] data)
        {
            // .ani is a RIFF container ("ACON") of CUR frames; Roblox cursors are
            // static, so extract the first frame (the default) and decode it.
            byte[]? frame = ExtractAniFrame(data);
            return frame is null ? null : IconToPng(frame);
        }

        private static byte[]? ExtractAniFrame(ReadOnlySpan<byte> data)
        {
            if (data.Length < 12 ||
                data[0] != (byte)'R' || data[1] != (byte)'I' ||
                data[2] != (byte)'F' || data[3] != (byte)'F')
                return null;

            int pos = 12; // skip "RIFF" <size> "ACON"
            while (pos + 8 <= data.Length)
            {
                ReadOnlySpan<byte> id = data.Slice(pos, 4);
                int size = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(pos + 4, 4));
                if (size < 0 || pos + 8 + size > data.Length) return null;

                int body = pos + 8;

                if (id.SequenceEqual("icon"u8))
                    return data.Slice(body, size).ToArray();

                if (id.SequenceEqual("LIST"u8) && size >= 4 &&
                    data.Slice(body, 4).SequenceEqual("fram"u8))
                {
                    int p = body + 4;
                    int end = body + size;
                    while (p + 8 <= end)
                    {
                        int frameSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(p + 4, 4));
                        if (frameSize < 0 || p + 8 + frameSize > end) return null;

                        if (data.Slice(p, 4).SequenceEqual("icon"u8))
                            return data.Slice(p + 8, frameSize).ToArray();

                        p += 8 + frameSize + (frameSize & 1); // RIFF chunks are word-aligned
                    }
                }

                pos = body + size + (size & 1);
            }

            return null;
        }

        private static byte[]? EncodePng(SKBitmap? bitmap)
        {
            if (bitmap is null) return null;

            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded?.ToArray();
        }
    }
}
