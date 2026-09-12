// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal partial class QuickPlayPage : UserControl
    {
        public QuickPlayPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Quick Play");

            GameSearchAutoCompleteBox.SelectionChanged += OnGameSearchSelectionChanged;
        }

        private void OnGameSearchSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is QuickPlayViewModel vm &&
                e.AddedItems.Count > 0 &&
                e.AddedItems[0] is OmniSearchContent selected)
            {
                vm.SearchQuery = selected.RootPlaceId.ToString(CultureInfo.InvariantCulture);
                vm.IsSearchFlyoutOpen = false;
            }
        }
    }
}