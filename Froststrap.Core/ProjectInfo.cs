// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap
{
    internal static class ProjectInfo
    {
        public const string Name = "Froststrap";

        public const string Repository = "Froststrap/Froststrap";

        public const string RemoteDataUrl = "https://raw.githubusercontent.com/Froststrap/config/refs/heads/main/Data.json";

        public const string BundleId = "xyz.froststrap.desktop";

        public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{Name}";

        public const string RegistryKey = $@"Software\{Name}";
    }
}
