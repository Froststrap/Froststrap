// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;

namespace Froststrap.UI.Elements.Settings.Pages;

internal partial class ChannelPage : UserControl
{
    public ChannelPage()
    {
        InitializeComponent();

        App.FrostRPC?.SetPage("Deployment");
    }
}