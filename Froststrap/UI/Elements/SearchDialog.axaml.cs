// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Froststrap.UI.ViewModels;

namespace Froststrap.UI.Elements;

internal partial class SearchDialog : Window
{
    private SearchBarViewModel? _viewModel;

    public SearchDialog()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    public SearchDialog(SearchBarViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SearchResultSelected += OnSearchResultSelected;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (Owner is Window owner)
        {
            Position = owner.PointToScreen(new Point(0, 0));
            Width = owner.Bounds.Width;
            Height = owner.Bounds.Height;
        }

        SearchBox.Focus();
    }

    private void OnSearchResultSelected(object? sender, SearchBarItem item)
    {
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Close();
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel?.SearchResultSelected -= OnSearchResultSelected;
        _viewModel = null;

        base.OnClosed(e);
    }
}