// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Localisation
{
    public static class Translations
    {
        public static string Get(string key) => Localizer.Get(key);
        public static string? TryGet(string key) => Localizer.TryGet(key);
        public static string GetEnglish(string key) => Localizer.GetEnglish(key);
    }
}
