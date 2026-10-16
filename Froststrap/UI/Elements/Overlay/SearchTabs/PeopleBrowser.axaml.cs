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
        private const double LoadMoreThreshold = 160;

        public PeopleBrowser()
        {
            InitializeComponent();

            PersonList.ContainerPrepared += OnPersonContainerPrepared;
            PersonList.AddHandler(ScrollViewer.ScrollChangedEvent, OnPersonListScrolled);
        }

        private static void OnPersonContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            if (e.Container.DataContext is SearchPerson person)
                person.EnsureHeadshotLoaded();
        }

        private void OnPersonListScrolled(object? sender, ScrollChangedEventArgs e)
        {
            if (e.Source is not ScrollViewer viewer || viewer.Extent.Height <= 0)
                return;

            if (viewer.Offset.Y + viewer.Viewport.Height < viewer.Extent.Height - LoadMoreThreshold)
                return;

            if (DataContext is ViewModels.Overlay.SearchTabs.PeopleBrowserViewModel vm)
                vm.LoadMorePeople();
        }

        private void PersonDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is not ViewModels.Overlay.SearchTabs.PeopleBrowserViewModel vm)
                return;

            vm.JoinSelected();
        }
    }
}
