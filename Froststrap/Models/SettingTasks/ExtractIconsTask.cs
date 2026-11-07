// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Froststrap.Utility;

namespace Froststrap.Models.SettingTasks
{
    internal class ExtractIconsTask : BoolBaseTask
    {
        private static string IconPath => Path.Combine(Paths.Base, Strings.Paths_Icons);

        // List of embedded icon resource names to extract
        private static readonly string[] AllowedIconNames =
        [
            "Icon2008",
            "Icon2011",
            "Icon2017",
            "Icon2019",
            "Icon2022",
            "Icon2025",
            "IconFroststrap",
            "IconEarly2015",
            "IconLate2015"
        ];

        public ExtractIconsTask() : base("ExtractIcons")
        {
            OriginalState = Directory.Exists(IconPath);
        }

        public override void Execute()
        {
            if (NewState)
            {
                Directory.CreateDirectory(IconPath);

                foreach (string iconName in AllowedIconNames)
                {
                    var assetUri = new Uri($"avares://Froststrap/Resources/{iconName}.png");

                    if (!AssetLoader.Exists(assetUri))
                        continue;

                    string filePath = Path.Combine(IconPath, $"{iconName}.ico");
                    Filesystem.AssertReadOnly(filePath);

                    using var stream = AssetLoader.Open(assetUri);
                    using var bitmap = new Bitmap(stream);
                    using var fileStream = File.Create(filePath);
                    IcoWriter.Write(bitmap, fileStream);
                }
            }
            else if (Directory.Exists(IconPath))
            {
                Directory.Delete(IconPath, true);
            }

            OriginalState = NewState;
        }
    }
}
