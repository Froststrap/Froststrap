using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Froststrap.UI.ViewModels;

namespace Froststrap.UI.Elements;

public partial class SearchBar : UserControl
{
    public SearchBar()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 50)
            {
                if (DataContext is SearchBarViewModel vm && vm.CanLoadMore)
                {
                    vm.LoadMoreGamesCommand.Execute(null);
                }
            }
        }
    }
}