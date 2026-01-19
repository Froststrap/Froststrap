using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Froststrap.Extensions
{
    static class BootstrapperIconEx
    {
        public static IReadOnlyCollection<BootstrapperIcon> Selections => new BootstrapperIcon[]
        {
            BootstrapperIcon.IconFroststrap,
            BootstrapperIcon.Icon2025,
            BootstrapperIcon.Icon2022,
            BootstrapperIcon.Icon2019,
            BootstrapperIcon.Icon2017,
            BootstrapperIcon.IconLate2015,
            BootstrapperIcon.IconEarly2015,
            BootstrapperIcon.Icon2011,
            BootstrapperIcon.Icon2008,
            BootstrapperIcon.IconFroststrapClassic,
            BootstrapperIcon.IconCustom
        };

        private static Dictionary<BootstrapperIcon, Bitmap> _cache = new();

        public static Bitmap GetIcon(this BootstrapperIcon icon)
        {
            const string LOG_IDENT = "BootstrapperIconEx::GetIcon";

            if (_cache.TryGetValue(icon, out var cached))
                return cached;

            // Load the custom icon file
            if (icon == BootstrapperIcon.IconCustom)
            {
                Bitmap? customIcon = null;
                string location = App.Settings.Prop.BootstrapperIconCustomLocation;

                if (string.IsNullOrEmpty(location))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Warning: custom icon is not set.");
                }
                else
                {
                    try
                    {
                        customIcon = LoadIconFromFile(location);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to load custom icon!");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }

                var result = customIcon ?? LoadFromResource("IconFroststrap");
                _cache[icon] = result;
                return result;
            }

            var bitmap = icon switch
            {
                BootstrapperIcon.IconFroststrap => LoadFromResource("IconFroststrap"),
                BootstrapperIcon.Icon2008 => LoadFromResource("Icon2008"),
                BootstrapperIcon.Icon2011 => LoadFromResource("Icon2011"),
                BootstrapperIcon.IconEarly2015 => LoadFromResource("IconEarly2015"),
                BootstrapperIcon.IconLate2015 => LoadFromResource("IconLate2015"),
                BootstrapperIcon.Icon2017 => LoadFromResource("Icon2017"),
                BootstrapperIcon.Icon2019 => LoadFromResource("Icon2019"),
                BootstrapperIcon.Icon2022 => LoadFromResource("Icon2022"),
                BootstrapperIcon.Icon2025 => LoadFromResource("Icon2025"),
                BootstrapperIcon.IconFroststrapClassic => LoadFromResource("IconFroststrapClassic"),
                _ => LoadFromResource("IconFroststrap")
            };

            _cache[icon] = bitmap;
            return bitmap;
        }

        private static Bitmap LoadFromResource(string name)
        {
            // Load the ICO file
            var uri = new Uri($"avares://Froststrap/Resources/{name}.ico");
            using var stream = AssetLoader.Open(uri);
            return LoadBestIconFromIcoStream(stream);
        }

        private static Bitmap LoadIconFromFile(string path)
        {
            using var stream = File.OpenRead(path);
            return LoadBestIconFromIcoStream(stream);
        }

        private static Bitmap LoadBestIconFromIcoStream(Stream stream)
        {
            stream.Position = 0;
            return new Bitmap(stream);
        }
    }
}