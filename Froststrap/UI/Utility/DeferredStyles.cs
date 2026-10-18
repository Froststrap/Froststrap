// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace Froststrap.UI.Utility
{
    internal static class DeferredStyles
    {
        private static bool _avaloniaEditThemeLoaded;
        private static bool _colorPickerTemplatesLoaded;

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Runtime XAML resource loading by URI; Froststrap and the referenced control assemblies are rooted so the compiled XAML is preserved.")]
        public static object Load(Uri uri) => AvaloniaXamlLoader.Load(uri);

        public static void EnsureAvaloniaEditTheme()
        {
            if (_avaloniaEditThemeLoaded || Application.Current is not { } app)
                return;

            _avaloniaEditThemeLoaded = true;

            if (Load(new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml")) is not Styles styles)
                return;

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

            app.Styles.Insert(insertAt, styles);
        }

        public static void EnsureColorPickerTemplates()
        {
            if (_colorPickerTemplatesLoaded || Application.Current is not { } app)
                return;

            _colorPickerTemplatesLoaded = true;

            string[] sources =
            [
                "avares://ColorPicker/Templates/HueSlider.axaml",
                "avares://ColorPicker/Templates/SquareSlider.axaml",
                "avares://ColorPicker/Templates/SquarePicker.axaml"
            ];

            foreach (string source in sources)
            {
                if (Load(new Uri(source)) is ResourceDictionary dictionary)
                    app.Resources.MergedDictionaries.Add(dictionary);
            }
        }
    }
}
