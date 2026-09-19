// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Froststrap.UI.ViewModels;

namespace Froststrap.UI.Elements;

internal partial class SearchDialog : UserControl
{
    private SearchBarViewModel? _viewModel;

    public event EventHandler? Dismissed;

    public SearchDialog()
    {
        InitializeComponent();
    }

    public SearchDialog(SearchBarViewModel viewModel) : this()
    {
        Subscribe(viewModel);
    }

    public void Subscribe(SearchBarViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SearchResultSelected += OnSearchResultSelected;
    }

    public void Unsubscribe()
    {
        if (_viewModel != null)
            _viewModel.SearchResultSelected -= OnSearchResultSelected;
        _viewModel = null;
    }

    public void Show()
    {
        IsVisible = true;

        Dispatcher.UIThread.Post(() =>
        {
            SearchBox.Focus();
        }, DispatcherPriority.Input);
    }

    public void Hide()
    {
        IsVisible = false;
        _viewModel?.Clear();
    }

    private void OnSearchResultSelected(object? sender, SearchBarItem item)
    {
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Dismissed?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}