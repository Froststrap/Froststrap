using System.Security.Cryptography;

namespace Froststrap.Utility
{
    internal static class SHA256Hash
    {
        public static string FromBytes(byte[] data)
        {
            byte[] hash = SHA256.HashData(data);
            return Stringify(hash);
        }

        public static string FromBytes(ReadOnlySpan<byte> data)
        {
            byte[] hash = SHA256.HashData(data);
            return Stringify(hash);
        }

        public static string FromStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);

            byte[] hash = SHA256.HashData(stream);
            return Stringify(hash);
        }

        public static string FromFile(string filename)
        {
            using FileStream stream = File.OpenRead(filename);
            return FromStream(stream);
        }

        public static string Stringify(byte[] hash)
        {
            return Convert.ToHexStringLower(hash);
        }

        public static string FromString(string str)
        {
            return FromBytes(Encoding.UTF8.GetBytes(str));
        }
    }
}