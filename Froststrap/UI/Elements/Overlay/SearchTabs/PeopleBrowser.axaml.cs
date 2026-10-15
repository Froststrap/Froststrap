// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Froststrap.Models.Overlay;

namespace Froststrap.UI.Elements.Overlay.SearchTabs
{
    internal partial class PeopleBrowser : UserControl
    {
        public PeopleBrowser()
        {
            InitializeComponent();

            PersonList.ContainerPrepared += OnPersonContainerPrepared;
        }

        private static void OnPersonContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            if (e.Container.DataContext is SearchPerson person)
                person.EnsureHeadshotLoaded();
        }

        private void PersonDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is not ViewModels.Overlay.SearchTabs.PeopleBrowserViewModel vm)
                return;

            vm.JoinSelected();
        }
    }
}
