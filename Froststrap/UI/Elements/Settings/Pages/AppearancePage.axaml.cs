// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages;

internal sealed partial class AppearancePage : UserControl
{
    public AppearancePage()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) => { if (!MainWindow.IsStagingPages) App.FrostRPC?.SetPage("Appearance"); };
    }
}
