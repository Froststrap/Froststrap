using System.IO.Hashing;

namespace Froststrap.Utility
{
    internal static class FastHash
    {
        public static string FromBytes(byte[] data)
        {
            ulong hash = XxHash64.HashToUInt64(data);
            return Stringify(hash);
        }

        public static string FromBytes(ReadOnlySpan<byte> data)
        {
            ulong hash = XxHash64.HashToUInt64(data);
            return Stringify(hash);
        }

        public static string FromStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var hasher = new XxHash64();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hasher.Append(buffer.AsSpan(0, read));
            }
            ulong hash = hasher.GetCurrentHashAsUInt64();
            return Stringify(hash);
        }

        public static string FromFile(string filename)
        {
            using FileStream stream = File.OpenRead(filename);
            return FromStream(stream);
        }

        public static string FromString(string str)
        {
            return FromBytes(Encoding.UTF8.GetBytes(str));
        }

        private static string Stringify(ulong hash)
        {
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}