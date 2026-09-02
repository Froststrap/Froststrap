using Avalonia.Controls;
using Froststrap.UI.ViewModels;

namespace Froststrap.UI.Elements;

internal partial class SearchBar : UserControl
{
    public SearchBar()
    {
        InitializeComponent();
        SearchAutoCompleteBox.SelectionChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SearchBarViewModel vm &&
            e.AddedItems.Count > 0 &&
            e.AddedItems[0] is SearchBarItem item)
        {
            vm.SearchResultSelectedCommand?.Execute(item);
        }
    }
}