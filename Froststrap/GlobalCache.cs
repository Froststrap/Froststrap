namespace Froststrap
{
    public static class GlobalCache
    {
        public static readonly Dictionary<string, string?> ServerLocation = [];

        public static readonly Dictionary<string, DateTime?> ServerTime = [];
    }
}
