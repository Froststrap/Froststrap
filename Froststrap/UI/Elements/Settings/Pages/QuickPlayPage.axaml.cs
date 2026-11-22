// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;
using System.ComponentModel;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal sealed partial class QuickPlayPage : UserControl
    {
        private QuickPlayViewModel? _viewModel;

        public QuickPlayPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Quick Play");

            GameSearchAutoCompleteBox.SelectionChanged += OnGameSearchSelectionChanged;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_viewModel is not null)
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

            _viewModel = DataContext as QuickPlayViewModel;

            if (_viewModel is not null)
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(QuickPlayViewModel.IsGamePageOpen) || _viewModel is not { } vm)
                return;

            if (vm.IsGamePageOpen && vm.GamePageItem is { } item)
                GamePage.Load(item, () => vm.CloseGamePageCommand.Execute(null), vm.InvalidateFavorites);
            else
                GamePage.Unload();
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
