using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Froststrap.UI.Elements.Base;

namespace Froststrap
{
    public static class Locale
    {
        public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;

        public static bool RightToLeft { get; private set; } = false;

        private static readonly List<string> _rtlLocales = ["ar", "he"];

        public static readonly Dictionary<string, string> SupportedLocales = new()
        {
            { "nil", Strings.Common_SystemDefault },
            { "en", "English" },
            { "en-US", "English (United States)" },
            { "sq", "Albanian" }, // Albanian
            { "ar", "العربية" }, // Arabic
            { "bg", "Български" }, // Bulgarian
            { "bn", "বাংলা" }, // Bengali
            { "bs", "Bosanski" }, // Bosnian
            { "cs", "Čeština" }, // Czech
            { "de", "Deutsch" }, // German
            { "da", "Dansk" }, // Danish
            { "es-ES", "Español" }, // Spanish
            { "el", "Ελληνικά" }, // Greek
            { "fi", "Suomi" }, // Finnish
            { "fr", "Français" }, // French
            { "he", "עברית‎" }, // Hebrew
            { "hi", "Hindi (Latin)" }, // Hindi
            { "hu", "Magyar" }, // Hungarian
            { "id", "Bahasa Indonesia" }, // Indonesian
            { "it", "Italiano" }, // Italian
            { "ja", "日本語" }, // Japanese
            { "ko", "한국어" }, // Korean
            { "lt", "Lietuvių" }, // Lithuanian
            { "nl", "Nederlands" }, // Dutch
            { "no", "Bokmål" }, // Norwegian
            { "pl", "Polski" }, // Polish
            { "pt-BR", "Português (Brasil)" }, // Portuguese, Brazilian
            { "ro", "Română" }, // Romanian
            { "ru", "Русский" }, // Russian
            { "sv-SE", "Svenska" }, // Swedish
            { "tr", "Türkçe" }, // Turkish
            { "uk", "Українська" }, // Ukrainian
            { "vi", "Tiếng Việt" }, // Vietnamese
            { "zh-CN", "中文 (简体)" }, // Chinese Simplified
            { "zh-HK", "中文 (廣東話)" }, // Chinese Traditional, Hong Kong
            { "zh-TW", "中文 (繁體)" } // Chinese Traditional
        };

        public static string GetIdentifierFromName(string language) => SupportedLocales.FirstOrDefault(x => x.Value == language).Key ?? "nil";

        public static List<string> GetLanguages()
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

        public static void Initialize()
        {
            Set("nil");

            ApplyLocaleToApplication();
        }

        public static void ApplyLocaleToWindow(Window window)
        {
            if (RightToLeft)
            {
                window.FlowDirection = FlowDirection.RightToLeft;
            }
            else if (CurrentCulture.Name.StartsWith("th"))
            {
                Application.Current?.Resources["ContentFontFamily"] =
                    new Avalonia.Media.FontFamily("Noto Sans Thai");
            }

#if QA_BUILD
            window.BorderBrush = Brushes.Red;
            window.BorderThickness = new Thickness(4);
#endif
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