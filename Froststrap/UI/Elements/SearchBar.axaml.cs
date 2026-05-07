using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Froststrap.UI.ViewModels;

namespace Froststrap.UI.Elements;

public partial class SearchBar : UserControl
{
    public SearchBar()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            if (DataContext is SearchBarViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SearchBarViewModel.IsSearchFlyoutOpen) && sender is SearchBarViewModel vm)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (SearchTextBox == null || SearchTextBox.GetVisualRoot() == null)
                    return;

                var flyout = FlyoutBase.GetAttachedFlyout(SearchTextBox);

                if (vm.IsSearchFlyoutOpen)
                {
                    if (flyout != null && !flyout.IsOpen)
                    {
                        FlyoutBase.ShowAttachedFlyout(SearchTextBox);
                    }
                }
                else
                {
                    flyout?.Hide();
                }
            });
        }
    }

    private void OnSearchIconClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var flyout = FlyoutBase.GetAttachedFlyout(SearchTextBox);
        flyout?.ShowAt(SearchTextBox);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 50)
            {
                if (DataContext is SearchBarViewModel vm && vm.CanLoadMore && !vm.IsGameSearchLoading)
                {
                    vm.LoadMoreGamesCommand.Execute(null);
                }
            }
        }
    }
}