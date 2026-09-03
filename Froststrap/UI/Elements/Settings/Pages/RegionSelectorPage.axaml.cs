using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal partial class RegionSelectorPage : UserControl
    {
        public RegionSelectorPage()
        {
            InitializeComponent();

            App.FrostRPC?.SetPage("Region Selector");

            GameSearchAutoCompleteBox.SelectionChanged += OnGameSearchSelectionChanged;
        }

        private void OnGameSearchSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is RegionSelectorViewModel vm &&
                e.AddedItems.Count > 0 &&
                e.AddedItems[0] is OmniSearchContent selected)
            {
                vm.PlaceId = selected.RootPlaceId.ToString(CultureInfo.InvariantCulture);
                vm.SearchQuery = selected.RootPlaceId.ToString(CultureInfo.InvariantCulture);
                vm.IsSearchFlyoutOpen = false;
                if (vm.SearchCommand.CanExecute(null))
                    _ = vm.SearchCommand.ExecuteAsync(null);
            }
        }
    }
}