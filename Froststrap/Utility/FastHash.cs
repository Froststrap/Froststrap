using System.Security.Cryptography;
using System.Text;

namespace Froststrap.Utility
{
    internal static class FastHash
    {
        public static string FromBytes(byte[] data)
        {
            byte[] hashBytes = MD5.HashData(data);
            return Stringify(hashBytes);
        }

        public static string FromBytes(ReadOnlySpan<byte> data)
        {
            // MD5 hashes are always 16 bytes long
            Span<byte> hashBytes = stackalloc byte[16];
            MD5.HashData(data, hashBytes);
            return Stringify(hashBytes);
        }

        public static string FromStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);

            // ComputeHash handles reading the stream to the end automatically
            using var md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(stream);
            
            return Stringify(hashBytes);
            
            // Note: If you are targeting .NET 7 or newer, you can replace the two lines above with:
            // byte[] hashBytes = MD5.HashData(stream);
            // return Stringify(hashBytes);
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

        private static string Stringify(byte[] hash)
        {
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string Stringify(ReadOnlySpan<byte> hash)
        {
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}