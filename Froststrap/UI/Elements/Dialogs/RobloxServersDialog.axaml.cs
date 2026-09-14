// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Input;
using Avalonia.Interactivity;
using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs;

internal partial class RobloxServersDialog : Base.AvaloniaWindow
{
    protected override bool ApplyTopPadding => false;

    public RobloxServersDialog()
    {
        InitializeComponent();
    }

    public RobloxServersDialog(RobloxServersDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        (DataContext as IDisposable)?.Dispose();
    }
}