// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages;

internal partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();

        DetachedFromVisualTree += (_, _) =>
        {
            (DataContext as IDisposable)?.Dispose();
        };
    }
}
