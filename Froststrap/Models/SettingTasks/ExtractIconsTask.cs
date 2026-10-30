// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Platform;
using System.Reflection;

namespace Froststrap.Models.SettingTasks
{
    internal class ExtractIconsTask : BoolBaseTask
    {
        private static string IconPath => Path.Combine(Paths.Base, Localizer.Instance.Get("Paths_Icons"));

        // List of embedded icon resource names to extract
        private static readonly string[] AllowedIconNames =
        [
            "Icon2008.ico",
            "Icon2011.ico",
            "Icon2017.ico",
            "Icon2019.ico",
            "Icon2022.ico",
            "Icon2025.ico",
            "IconFroststrap.ico",
            "IconEarly2015.ico",
            "IconLate2015.ico"
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
                    var assetUri = new Uri($"avares://Froststrap/Resources/{iconName}");

                    if (!AssetLoader.Exists(assetUri))
                        continue;

                    string filePath = Path.Combine(IconPath, iconName);
                    Filesystem.AssertReadOnly(filePath);

                    using var stream = AssetLoader.Open(assetUri);
                    using var fileStream = File.Create(filePath);
                    stream.CopyTo(fileStream);
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
