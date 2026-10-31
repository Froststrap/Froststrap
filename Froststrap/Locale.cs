// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Froststrap.Localisation;
using System.Globalization;
using System.IO;

namespace Froststrap
{
    internal static class Locale
    {
        public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;

        public static bool RightToLeft { get; private set; }

        private static readonly List<string> _rtlLocales = ["ar", "he"];

        private static readonly Dictionary<string, string> _legacyToFull = new()
        {
            { "en", "en-US" }, { "de", "de-DE" }, { "fr", "fr-FR" }, { "ja", "ja-JP" },
            { "pt-BR", "pt-BR" }, { "pt-PT", "pt-PT" }, { "zh-CN", "zh-CN" }, { "zh-TW", "zh-TW" },
            { "sv-SE", "sv-SE" }, { "es-ES", "es-ES" }, { "ar", "ar-SA" }, { "bg", "bg-BG" },
            { "cs", "cs-CZ" }, { "da", "da-DK" }, { "el", "el-GR" }, { "et", "et-EE" },
            { "fi", "fi-FI" }, { "hu", "hu-HU" }, { "id", "id-ID" }, { "it", "it-IT" },
            { "ko", "ko-KR" }, { "lt", "lt-LT" }, { "lv", "lv-LV" }, { "nl", "nl-NL" },
            { "pl", "pl-PL" }, { "ro", "ro-RO" }, { "ru", "ru-RU" }, { "sk", "sk-SK" },
            { "sl", "sl-SI" }, { "tr", "tr-TR" }, { "uk", "uk-UA" }, { "vi", "vi-VN" }
        };

        private static Dictionary<string, string> _supportedLocales;
        public static Dictionary<string, string> SupportedLocales => _supportedLocales ??= new()
        {
            { "nil", Localizer.Instance.Get("Common_SystemDefault") },
            { "en-US", "English (United States)" },
            { "ar-SA", "العربية" },
            { "bg-BG", "Български" },
            { "cs-CZ", "Čeština" },
            { "da-DK", "Dansk" },
            { "de-DE", "Deutsch" },
            { "el-GR", "Ελληνικά" },
            { "es-ES", "Español" },
            { "et-EE", "Eesti" },
            { "fi-FI", "Suomi" },
            { "fr-FR", "Français" },
            { "hu-HU", "Magyar" },
            { "id-ID", "Bahasa Indonesia" },
            { "it-IT", "Italiano" },
            { "ja-JP", "日本語" },
            { "ko-KR", "한국어" },
            { "lt-LT", "Lietuvių" },
            { "lv-LV", "Latviešu" },
            { "nl-NL", "Nederlands" },
            { "pl-PL", "Polski" },
            { "pt-BR", "Português (Brasil)" },
            { "pt-PT", "Português (Portugal)" },
            { "ro-RO", "Română" },
            { "ru-RU", "Русский" },
            { "sk-SK", "Slovenčina" },
            { "sl-SI", "Slovenščina" },
            { "sv-SE", "Svenska" },
            { "tr-TR", "Türkçe" },
            { "uk-UA", "Українська" },
            { "vi-VN", "Tiếng Việt" },
            { "zh-CN", "中文 (简体)" },
            { "zh-TW", "中文 (繁體)" }
        };

        public static string MigrateLegacySetting(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return "nil";
            if (identifier == "nil") return "nil";
            if (_legacyToFull.TryGetValue(identifier, out var full)) return full;
            if (SupportedLocales.ContainsKey(identifier)) return identifier;
            // Try two-letter prefix match for backward compatibility
            foreach (var k in SupportedLocales.Keys)
            {
                if (k != "nil" && k.StartsWith(identifier + "-", StringComparison.OrdinalIgnoreCase)) return k;
                if (k != "nil" && k.StartsWith(identifier, StringComparison.OrdinalIgnoreCase) && k.Length == identifier.Length + 3 && k[identifier.Length] == '-') return k;
            }
            return identifier;
        }

        public static string GetIdentifierFromName(string language) => SupportedLocales.FirstOrDefault(x => x.Value == language).Key ?? "nil";

        public static List<string> GetLanguages()
        {
            var languages = new List<string>();

            languages.AddRange(SupportedLocales.Values.Take(3));
            languages.AddRange(SupportedLocales.Values.Where(x => !languages.Contains(x)).OrderBy(x => x));
            languages[0] = Localizer.Instance.Get("Common_SystemDefault");

            return languages;
        }

        private static string ResolveDefaultLocale()
        {
            var name = CultureInfo.CurrentUICulture.Name;
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var k in SupportedLocales.Keys)
                {
                    if (k == "nil") continue;
                    if (k.Equals(name, StringComparison.OrdinalIgnoreCase)) return k;
                    // two-letter prefix match for backward compatibility
                    if (name.Length >= 2 && k.StartsWith(name.Substring(0, 2), StringComparison.OrdinalIgnoreCase) && (k.Length == name.Length || k[name.Length] == '-'))
                        return k;
                }
            }
            return "en-US";
        }

        public static void Set(string identifier)
        {
            identifier = MigrateLegacySetting(identifier);

            if (!SupportedLocales.ContainsKey(identifier))
            {
                if (identifier == "nil")
                {
                    identifier = ResolveDefaultLocale();
                }
                else
                {
                    identifier = "en-US";
                }
            }

            if (identifier == "nil")
                identifier = ResolveDefaultLocale();

            if (identifier == "nil")
                identifier = "en-US";

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

            Localizer.Instance.CultureName = CurrentCulture.Name;

            RightToLeft = _rtlLocales.Any(l => CurrentCulture.Name.StartsWith(l, StringComparison.OrdinalIgnoreCase));

            try
            {
                Localizer.Instance.CultureChanged += () =>
                {
                    try { System.Diagnostics.Debug.WriteLine("Locale changed - restart advised for static bindings"); } catch { }
                };
            }
            catch { }

            _ = Task.Run(async () =>
            {
                try
                {
                    await RemoteLocalisationService.RefreshAsync(CurrentCulture.Name);
                }
                catch { }
            });
        }

        public static void Initialize()
        {
            // Load default and culture translations from packaged JSON
            var baseDir = AppContext.BaseDirectory;
            var translationsDir = Path.Combine(baseDir, "Resources", "Translations");
            if (Directory.Exists(translationsDir))
            {
                var defaultPath = Path.Combine(translationsDir, "en-US.json");
                if (File.Exists(defaultPath))
                    Localizer.Instance.LoadDefault(defaultPath);

                foreach (var file in Directory.GetFiles(translationsDir, "*.json"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    Localizer.Instance.LoadCulture(name, file);
                }
            }

            Set("nil");

            ApplyLocaleToApplication();
        }

        public static void ApplyLocaleToWindow(Window window)
        {
            if (RightToLeft)
            {
                window.FlowDirection = FlowDirection.RightToLeft;
            }
            else if (CurrentCulture.Name.StartsWith("th", StringComparison.Ordinal))
            {
                Application.Current?.Resources["ContentFontFamily"] =
                    new Avalonia.Media.FontFamily("Noto Sans Thai");
            }
        }

        private static void ApplyLocaleToApplication()
        {
            if (RightToLeft)
            {
                var rtlStyle = new Style(x => x.OfType<Control>())
                {
                    Setters =
                    {
                        new Setter(Control.FlowDirectionProperty, FlowDirection.RightToLeft)
                    }
                };

                Application.Current?.Styles.Add(rtlStyle);
            }
        }
    }
}
