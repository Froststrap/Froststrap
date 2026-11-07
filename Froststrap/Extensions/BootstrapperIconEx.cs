// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Froststrap.Extensions
{
    static class BootstrapperIconEx
    {
        public static IReadOnlyCollection<BootstrapperIcon> Selections =>
        [
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
        ];

        private static readonly Dictionary<BootstrapperIcon, Bitmap> _cache = [];

        public static Bitmap GetIcon(this BootstrapperIcon icon)
        {
            if (_cache.TryGetValue(icon, out var cached))
                return cached;

            if (icon == BootstrapperIcon.IconCustom)
            {
                Bitmap? customIcon = null;
                string location = App.Settings.Prop.BootstrapperIconCustomLocation;

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
            var uri = new Uri($"avares://Froststrap/Resources/{name}.png");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }

        private static Bitmap LoadIconFromFile(string path)
        {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }

        public static void InvalidateCustomIcon()
        {
            _cache.Remove(BootstrapperIcon.IconCustom);
        }
    }
}