// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Globalization;

namespace Froststrap.Localisation
{
    public static class Translations
    {
        public static string Get(string key) => TryGet(key) ?? key;
        public static string? TryGet(string key) => Lookup(key, Strings.Culture ?? CultureInfo.CurrentUICulture);
        public static string GetEnglish(string key) => Lookup(key, CultureInfo.InvariantCulture) ?? key;
        private static string? Lookup(string key, CultureInfo culture) => Strings.ResourceManager.GetString(key, culture);
    }
}
