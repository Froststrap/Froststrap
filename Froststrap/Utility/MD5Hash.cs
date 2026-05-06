using System.Security.Cryptography;

namespace Froststrap.Utility
{
    public static class MD5Hash
    {
        public static string FromBytes(byte[] data)
        {
            byte[] hash = MD5.HashData(data);
            return Stringify(hash);
        }

        public static string FromStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);

            byte[] hash = MD5.HashData(stream);
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