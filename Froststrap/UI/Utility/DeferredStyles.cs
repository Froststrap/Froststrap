// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace Froststrap.UI.Utility
{
    internal static class DeferredStyles
    {
        private static bool _avaloniaEditThemeLoaded;

        public static void EnsureAvaloniaEditTheme()
        {
            if (_avaloniaEditThemeLoaded || Application.Current is not { } app)
                return;

            _avaloniaEditThemeLoaded = true;

            int insertAt = app.Styles.Count;

            for (int i = 0; i < app.Styles.Count; i++)
            {
                if (app.Styles[i] is StyleInclude include &&
                    include.Source?.OriginalString.EndsWith("AppThemes/Default.axaml", StringComparison.OrdinalIgnoreCase) == true)
                {
                    insertAt = i;
                    break;
                }
            }

            app.Styles.Insert(insertAt, new AvaloniaEditTheme());
        }
    }
}
