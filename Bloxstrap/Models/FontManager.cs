using System.Windows;

namespace Bloxstrap.Models
{
    public static class FontManager
    {
        public static bool IsCustomFontApplied { get; private set; }

        public static System.Windows.Media.FontFamily? LoadFontFromFile(string fontFilePath)
        {
            if (!File.Exists(fontFilePath))
                return null;

            string tempFontsRoot = Path.Combine(Path.GetTempPath(), "Froststrap", "Fonts");

            string uniqueFontFolder = Path.Combine(tempFontsRoot, Guid.NewGuid().ToString());
            Directory.CreateDirectory(uniqueFontFolder);

            string destFontPath = Path.Combine(uniqueFontFolder, Path.GetFileName(fontFilePath));
            File.Copy(fontFilePath, destFontPath, overwrite: true);

            var fontDirectoryUri = new Uri(uniqueFontFolder + Path.DirectorySeparatorChar);
            var fontFamilies = System.Windows.Media.Fonts.GetFontFamilies(fontDirectoryUri);

            return fontFamilies.FirstOrDefault();
        }

        public static bool ApplySavedCustomFont()
        {
            string? savedFontPath = App.Settings.Prop.CustomFontPath;

            if (!string.IsNullOrWhiteSpace(savedFontPath) && File.Exists(savedFontPath))
            {
                try
                {
                    var font = LoadFontFromFile(savedFontPath);
                    if (font != null)
                    {
                        ApplyFontGlobally(font);
                        IsCustomFontApplied = true;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("FontManager", $"Failed to load saved font: {ex}");
                }
            }

            return false;
        }

        public static void ApplyFontGlobally(System.Windows.Media.FontFamily fontFamily)
        {
            Application.Current.Resources[SystemFonts.MessageFontFamilyKey] = fontFamily;

            foreach (Window window in Application.Current.Windows)
                window.FontFamily = fontFamily;

            IsCustomFontApplied = fontFamily.Source != "Segoe UI";
        }

        public static void RemoveCustomFont()
        {
            var defaultFont = new System.Windows.Media.FontFamily("Segoe UI");
            ApplyFontGlobally(defaultFont);
            IsCustomFontApplied = false;
            App.Settings.Prop.CustomFontPath = null;
            App.Settings.Save();
        }
    }
}