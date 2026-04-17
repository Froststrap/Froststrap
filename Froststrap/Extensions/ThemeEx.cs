using Avalonia;
using Avalonia.Styling;
using Avalonia.Platform;

namespace Froststrap.Extensions
{
    public static class ThemeEx
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