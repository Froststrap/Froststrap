using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Froststrap.Extensions
{
    public static class ThemeEx
    {
        [SupportedOSPlatform("windows")]
        public static Theme GetFinal(this Theme dialogTheme)
        {
            if (dialogTheme != Theme.Default)
                return dialogTheme;

            using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value && value == 0)
                return Theme.Dark;

            return Theme.Light;
        }
    }
}