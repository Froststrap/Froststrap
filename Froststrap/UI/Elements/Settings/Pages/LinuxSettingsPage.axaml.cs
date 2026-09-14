// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;

namespace Froststrap.UI.Elements.Settings.Pages;

internal partial class LinuxSettingsPage : UserControl
{
    public LinuxSettingsPage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Sober Settings");
    }
}
