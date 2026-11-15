// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Globalization;

namespace Froststrap.Localisation
{
    public static class Locale
    {
        public const string SystemDefault = "nil";

        private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;

        public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;

        public static bool RightToLeft { get; private set; }

        private static readonly List<string> _rtlLocales = ["ar", "he"];

        public static readonly Dictionary<string, string> SupportedLocales = new()
        {
            { SystemDefault, Strings.Common_SystemDefault },
            { "en-US", "English" },
            { "ar-SA", "العربية" }, // Arabic
            { "bg-BG", "Български" }, // Bulgarian
            { "cs-CZ", "Čeština" }, // Czech
            { "da-DK", "Dansk" }, // Danish
            { "de-DE", "Deutsch" }, // German
            { "el-GR", "Ελληνικά" }, // Greek
            { "es-ES", "Español" }, // Spanish
            { "et-EE", "Eesti" }, // Estonian
            { "fi-FI", "Suomi" }, // Finnish
            { "fr-FR", "Français" }, // French
            { "hu-HU", "Magyar" }, // Hungarian
            { "id-ID", "Bahasa Indonesia" }, // Indonesian
            { "it-IT", "Italiano" }, // Italian
            { "ja-JP", "日本語" }, // Japanese
            { "ko-KR", "한국어" }, // Korean
            { "lt-LT", "Lietuvių" }, // Lithuanian
            { "lv-LV", "Latviešu" }, // Latvian
            { "nl-NL", "Nederlands" }, // Dutch
            { "pl-PL", "Polski" }, // Polish
            { "pt-BR", "Português (Brasil)" }, // Portuguese, Brazilian
            { "pt-PT", "Português (Portugal)" }, // Portuguese, Portugal
            { "ro-RO", "Română" }, // Romanian
            { "ru-RU", "Русский" }, // Russian
            { "sk-SK", "Slovenčina" }, // Slovak
            { "sl-SI", "Slovenščina" }, // Slovenian
            { "sv-SE", "Svenska" }, // Swedish
            { "tr-TR", "Türkçe" }, // Turkish
            { "uk-UA", "Українська" }, // Ukrainian
            { "vi-VN", "Tiếng Việt" }, // Vietnamese
            { "zh-CN", "中文 (简体)" }, // Chinese Simplified
            { "zh-TW", "中文 (繁體)" } // Chinese Traditional
        };

        public static string GetIdentifierFromName(string language) => SupportedLocales.FirstOrDefault(x => x.Value == language).Key ?? SystemDefault;

        public static IReadOnlyList<string> GetLanguages()
        {
            var languages = new List<string>();

            languages.AddRange(SupportedLocales.Values.Take(2));
            languages.AddRange(SupportedLocales.Values.Where(x => !languages.Contains(x)).OrderBy(x => x));
            languages[0] = Strings.Common_SystemDefault;

            return languages;
        }

        public static string Normalise(string? identifier)
        {
            if (string.IsNullOrEmpty(identifier) || identifier == SystemDefault)
                return SystemDefault;

            if (SupportedLocales.ContainsKey(identifier))
                return identifier;

            return Match(identifier) ?? SystemDefault;
        }

        public static void Set(string identifier)
        {
            identifier = Normalise(identifier);

            string locale = identifier == SystemDefault
                ? Match(SystemCulture.Name) ?? Localizer.SourceLocale
                : identifier;

            CurrentCulture = identifier == SystemDefault ? SystemCulture : new CultureInfo(identifier);

            Thread.CurrentThread.CurrentCulture = CurrentCulture;
            Thread.CurrentThread.CurrentUICulture = CurrentCulture;
            CultureInfo.DefaultThreadCurrentCulture = CurrentCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CurrentCulture;

            Localizer.SetCulture(locale);

            RightToLeft = _rtlLocales.Any(l => locale.StartsWith(l, StringComparison.OrdinalIgnoreCase));
        }

        private static string? Match(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            foreach (var key in SupportedLocales.Keys)
            {
                if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return key;
            }

            CultureInfo culture;

            try
            {
                culture = CultureInfo.GetCultureInfo(name);
            }
            catch (CultureNotFoundException)
            {
                return null;
            }

            if (culture.TwoLetterISOLanguageName == "zh")
                return IsTraditionalChinese(culture) ? "zh-TW" : "zh-CN";

            string prefix = culture.TwoLetterISOLanguageName + "-";

            return SupportedLocales.Keys.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTraditionalChinese(CultureInfo culture)
        {
            for (var current = culture; !current.Equals(CultureInfo.InvariantCulture); current = current.Parent)
            {
                if (current.Name.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return culture.Name is "zh-TW" or "zh-HK" or "zh-MO";
        }
    }
}
