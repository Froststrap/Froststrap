using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Froststrap.Extensions
{
    static class RobloxIconEx
    {
        public static IReadOnlyCollection<RobloxIcon> Selections => new RobloxIcon[]
        {
            RobloxIcon.Default,
            RobloxIcon.Icon2022,
            RobloxIcon.Icon2019,
            RobloxIcon.Icon2017,
            RobloxIcon.IconEarly2015,
            RobloxIcon.IconLate2015,
            RobloxIcon.Icon2011,
            RobloxIcon.Icon2008,
        };

        private static Dictionary<RobloxIcon, Bitmap> _cache = new();

        public static Bitmap GetIcon(this RobloxIcon icon)
        {
            if (_cache.TryGetValue(icon, out var cached))
                return cached;

            var bitmap = icon switch
            {
                RobloxIcon.IconFroststrap => LoadFromResource("IconFroststrap"),
                RobloxIcon.Icon2008 => LoadFromResource("Icon2008"),
                RobloxIcon.Icon2011 => LoadFromResource("Icon2011"),
                RobloxIcon.IconEarly2015 => LoadFromResource("IconEarly2015"),
                RobloxIcon.IconLate2015 => LoadFromResource("IconLate2015"),
                RobloxIcon.Icon2017 => LoadFromResource("Icon2017"),
                RobloxIcon.Icon2019 => LoadFromResource("Icon2019"),
                RobloxIcon.Icon2022 => LoadFromResource("Icon2022"),
                RobloxIcon.Default => LoadFromResource("Icon2025"),
                _ => LoadFromResource("IconFroststrap")
            };

            _cache[icon] = bitmap;
            return bitmap;
        }

        private static Bitmap LoadFromResource(string name)
        {
            var uri = new Uri($"avares://Froststrap/Assets/Icons/{name}.ico");
            using var stream = AssetLoader.Open(uri);
            stream.Position = 0;
            return new Bitmap(stream);
        }
    }
}