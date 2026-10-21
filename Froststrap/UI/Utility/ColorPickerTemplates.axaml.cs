// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Froststrap.UI.Utility
{
    internal partial class ColorPickerTemplates : ResourceDictionary
    {
        public ColorPickerTemplates()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
