using Microsoft.Win32;

namespace Froststrap.Extensions
{
    public static class ThemeEx
    {
        public static Theme GetFinal(this Theme dialogTheme)
        {
            if (dialogTheme != Theme.Default)
                return dialogTheme;

            if (OperatingSystem.IsWindows())
            {
                return GetWindowsSystemTheme();
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                return GetUnixSystemTheme();
            }

            return Theme.Light;
        }

        private static Theme GetWindowsSystemTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int value && value == 0)
                return Theme.Dark;

            return Theme.Light;
        }

        private static Theme GetUnixSystemTheme()
        {
            // TODO
            return Theme.Dark;
        }
    }
}