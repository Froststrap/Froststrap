using System;
using System.Collections.Generic;
using System.IO;
using AvaloniaFontFamily = Avalonia.Media.FontFamily;

namespace Froststrap.UI.Utility
{
    /// <summary>
    /// Manages custom theme font loading for Avalonia
    /// </summary>
    public static class ThemeFontManager
    {
        private static readonly Dictionary<string, AvaloniaFontFamily> RegisteredFonts = new();

        /// <summary>
        /// Registers fonts from a theme directory
        /// </summary>
        public static void RegisterThemeFonts(string themeDirectory)
        {
            if (!Directory.Exists(themeDirectory))
                return;

            try
            {
                // Find all font files in the theme directory
                var fontExtensions = new[] { "*.ttf", "*.otf" };
                var fontFiles = new List<string>();

                foreach (var pattern in fontExtensions)
                {
                    fontFiles.AddRange(Directory.GetFiles(themeDirectory, pattern));
                }

                // Register each font
                foreach (var fontFile in fontFiles)
                {
                    try
                    {
                        string fontName = Path.GetFileNameWithoutExtension(fontFile);
                        string normalizedPath = fontFile.Replace("\\", "/");
                        string fontUri = $"file:///{normalizedPath}#{fontName}";

                        // Create and cache the font family
                        var fontFamily = new AvaloniaFontFamily(fontUri);
                        RegisteredFonts[fontName] = fontFamily;

                        App.Logger?.WriteLine("ThemeFontManager", $"Registered font: {fontName} from {fontFile}");
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteException("ThemeFontManager", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ThemeFontManager", ex);
            }
        }

        /// <summary>
        /// Gets a registered font by name
        /// </summary>
        public static AvaloniaFontFamily? GetFont(string fontName)
        {
            return RegisteredFonts.TryGetValue(fontName, out var font) ? font : null;
        }

        /// <summary>
        /// Clears all registered theme fonts
        /// </summary>
        public static void ClearRegisteredFonts()
        {
            RegisteredFonts.Clear();
        }

        /// <summary>
        /// Converts a theme:// font URI to a FontFamily object
        /// </summary>
        public static string ResolveFontUri(string uri, string themeDirectory)
        {
            if (!uri.StartsWith("theme://#", StringComparison.OrdinalIgnoreCase))
                return uri;

            string fontName = uri.Substring("theme://#".Length);

            // First try registered fonts
            var font = GetFont(fontName);
            if (font is not null)
                return font.ToString();

            // Try to find and register the font
            foreach (var ext in new[] { ".ttf", ".otf" })
            {
                string fontPath = Path.Combine(themeDirectory, fontName + ext);
                if (File.Exists(fontPath))
                {
                    string normalizedPath = fontPath.Replace("\\", "/");
                    return $"file:///{normalizedPath}#{fontName}";
                }
            }

            // Fallback to just the font name
            return fontName;
        }
    }
}