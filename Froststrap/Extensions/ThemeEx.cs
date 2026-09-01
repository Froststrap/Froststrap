using Avalonia;
using Avalonia.Platform;

namespace Froststrap.Extensions
{
    internal static class ThemeEx
    {
        public static Theme GetFinal(this Theme dialogTheme)
        {
            if (dialogTheme != Theme.Default)
                return dialogTheme;

            var variant = Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant;
            return variant == PlatformThemeVariant.Dark ? Theme.Dark : Theme.Light;
        }
    }
}