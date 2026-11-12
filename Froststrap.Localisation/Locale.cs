// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Globalization;

namespace Froststrap.Localisation
{
    public static class Locale
    {
        public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;

        public static bool RightToLeft { get; private set; }

        private static readonly List<string> _rtlLocales = ["ar", "he"];

        public static readonly Dictionary<string, string> SupportedLocales = new()
        {
            { "nil", Strings.Common_SystemDefault },
            { "en", "English" },
            { "en-US", "English (United States)" },
            { "ar", "العربية" }, // Arabic
            { "bg", "Български" }, // Bulgarian
            { "cs", "Čeština" }, // Czech
            { "da", "Dansk" }, // Danish
            { "de", "Deutsch" }, // German
            { "el", "Ελληνικά" }, // Greek
            { "es-ES", "Español" }, // Spanish
            { "et", "Eesti" }, // Estonian
            { "fi", "Suomi" }, // Finnish
            { "fr", "Français" }, // French
            { "hu", "Magyar" }, // Hungarian
            { "id", "Bahasa Indonesia" }, // Indonesian
            { "it", "Italiano" }, // Italian
            { "ja", "日本語" }, // Japanese
            { "ko", "한국어" }, // Korean
            { "lt", "Lietuvių" }, // Lithuanian
            { "lv", "Latviešu" }, // Latvian
            { "nl", "Nederlands" }, // Dutch
            { "pl", "Polski" }, // Polish
            { "pt-BR", "Português (Brasil)" }, // Portuguese, Brazilian
            { "pt-PT", "Português (Portugal)" }, // Portuguese, Portugal
            { "ro", "Română" }, // Romanian
            { "ru", "Русский" }, // Russian
            { "sk", "Slovenčina" }, // Slovak
            { "sl", "Slovenščina" }, // Slovenian
            { "sv-SE", "Svenska" }, // Swedish
            { "tr", "Türkçe" }, // Turkish
            { "uk", "Українська" }, // Ukrainian
            { "vi", "Tiếng Việt" }, // Vietnamese
            { "zh-CN", "中文 (简体)" }, // Chinese Simplified
            { "zh-TW", "中文 (繁體)" } // Chinese Traditional
        };

        public static string GetIdentifierFromName(string language) => SupportedLocales.FirstOrDefault(x => x.Value == language).Key ?? "nil";

        public static IReadOnlyList<string> GetLanguages()
        {
            var languages = new List<string>();

            languages.AddRange(SupportedLocales.Values.Take(3));
            languages.AddRange(SupportedLocales.Values.Where(x => !languages.Contains(x)).OrderBy(x => x));
            languages[0] = Strings.Common_SystemDefault;

            return languages;
        }

        public static void Set(string identifier)
        {
            if (!SupportedLocales.ContainsKey(identifier))
                identifier = "nil";

            if (identifier == "nil")
            {
                CurrentCulture = CultureInfo.CurrentUICulture;
            }
            else
            {
                CurrentCulture = new CultureInfo(identifier);
            }

            Thread.CurrentThread.CurrentCulture = CurrentCulture;
            Thread.CurrentThread.CurrentUICulture = CurrentCulture;
            CultureInfo.DefaultThreadCurrentCulture = CurrentCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CurrentCulture;

            Strings.Culture = CurrentCulture;

            RightToLeft = _rtlLocales.Any(l => CurrentCulture.Name.StartsWith(l, StringComparison.OrdinalIgnoreCase));
        }
    }
}
