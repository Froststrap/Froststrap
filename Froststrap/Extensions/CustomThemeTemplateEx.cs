namespace Froststrap.Extensions
{
    static class CustomThemeTemplateEx
    {
        const string EXAMPLES_URL = "https://github.com/Bloxstraplabs/custom-bootstrapper-examples";

        public static string GetFileName(this CustomThemeTemplate template)
        {
            return $"CustomBootstrapperTemplate_{template}.xml";
        }

        public static async Task<string> GetFileContents(this CustomThemeTemplate template)
        {
            var resourceData = await Resource.Get(template.GetFileName());
            string contents = Encoding.UTF8.GetString(resourceData);

            switch (template)
            {
                case CustomThemeTemplate.Blank:
                    string moreTextBlank = string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.CustomTheme_Templates_Blank_MoreExamples,
                        EXAMPLES_URL);

                    return contents
                        .Replace("{0}", Strings.CustomTheme_Templates_Blank_UIElements, StringComparison.Ordinal)
                        .Replace("{1}", moreTextBlank, StringComparison.Ordinal);

                case CustomThemeTemplate.Simple:
                    string moreTextSimple = string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.CustomTheme_Templates_Simple_MoreExamples,
                        EXAMPLES_URL);

                    return contents.Replace("{0}", moreTextSimple, StringComparison.Ordinal);

                default:
                    return contents;
            }
        }
    }
}