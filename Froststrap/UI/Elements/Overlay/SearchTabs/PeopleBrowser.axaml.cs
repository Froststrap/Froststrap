// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Froststrap.UI.Elements.Overlay.SearchTabs
{
    internal partial class PeopleBrowser : UserControl
    {
        public PeopleBrowser()
        {
            InitializeComponent();
        }

        private void PersonDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is not ViewModels.Overlay.SearchTabs.PeopleBrowserViewModel vm)
                return;

            vm.JoinSelected();
        }
    }
}