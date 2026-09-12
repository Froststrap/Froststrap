// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal partial class QuickPlayPage : UserControl
    {
        private Popup? _searchDropDownPopup;

        public QuickPlayPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Quick Play");

            GameSearchAutoCompleteBox.SelectionChanged += OnGameSearchSelectionChanged;
            GameSearchAutoCompleteBox.TemplateApplied += OnGameSearchTemplateApplied;
            GameSearchAutoCompleteBox.SizeChanged += OnGameSearchSizeChanged;
        }

        private void OnGameSearchTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            _searchDropDownPopup = GameSearchAutoCompleteBox
                .GetVisualDescendants()
                .OfType<Popup>()
                .FirstOrDefault();

            _searchDropDownPopup?.Opened += OnSearchDropDownOpened;
        }

        private void OnSearchDropDownOpened(object? sender, EventArgs e)
            => ApplySearchDropDownWidth();

        private void OnGameSearchSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_searchDropDownPopup is { IsOpen: true })
                ApplySearchDropDownWidth();
        }

        private void ApplySearchDropDownWidth()
        {
            if (_searchDropDownPopup?.Child is not Control child) return;

            double targetWidth = GameSearchAutoCompleteBox.Bounds.Width;
            if (targetWidth > 0)
                child.Width = targetWidth;
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