// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Input;
using Froststrap.UI.Elements.Base;
using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs;

internal sealed partial class PlaytimeStatsDialog : AvaloniaWindow
{
    public PlaytimeStatsDialog()
    {
        InitializeComponent();

        // Opened (not ctor): if the opener assigns DataContext after construction,
        // we don't create a second, wasted ViewModel.
        Opened += OnDialogOpened;
        Closed += OnDialogClosed;
    }

    private void OnDialogOpened(object? sender, EventArgs e)
    {
        if (DataContext is not PlaytimeStatsDialogViewModel)
            DataContext = new PlaytimeStatsDialogViewModel();
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        // Unsubscribes from AccountManager.ActiveAccountChanged. Dispose is idempotent.
        (DataContext as IDisposable)?.Dispose();
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
